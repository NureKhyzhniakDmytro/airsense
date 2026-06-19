from __future__ import annotations

import json
import logging
from datetime import datetime, timezone

import joblib
import numpy as np
import pandas as pd
from psycopg.types.json import Jsonb
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import mean_absolute_error, mean_squared_error

from .db import connect, ensure_schema
from .model_store import FEATURE_COLUMNS, HORIZONS, TARGET_COLUMNS, model_path, save_heuristic_artifact


logging.basicConfig(level="INFO", format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger("ai-training-job")


def load_telemetry() -> pd.DataFrame:
    query = """
        WITH sensor_points AS (
            SELECT
                s.room_id,
                date_trunc('minute', sd.timestamp) AS timestamp,
                p.name AS parameter,
                AVG(sd.value) AS value
            FROM sensor_data sd
            JOIN sensors s ON s.id = sd.sensor_id
            JOIN parameters p ON p.id = sd.parameter_id
            WHERE s.room_id IS NOT NULL
              AND p.name IN ('co2', 'temperature', 'humidity', 'occupancy')
            GROUP BY s.room_id, date_trunc('minute', sd.timestamp), p.name
        ),
        pivoted AS (
            SELECT
                room_id,
                timestamp,
                AVG(value) FILTER (WHERE parameter = 'co2') AS co2,
                AVG(value) FILTER (WHERE parameter = 'temperature') AS temperature,
                AVG(value) FILTER (WHERE parameter = 'humidity') AS humidity,
                COALESCE(AVG(value) FILTER (WHERE parameter = 'occupancy'), 0) AS occupancy
            FROM sensor_points
            GROUP BY room_id, timestamp
        )
        SELECT
            p.room_id,
            p.timestamp,
            p.co2,
            p.temperature,
            p.humidity,
            COALESCE((
                SELECT dd.value
                FROM devices d
                JOIN device_data dd ON dd.device_id = d.id
                WHERE d.room_id = p.room_id
                  AND dd.timestamp <= p.timestamp
                ORDER BY dd.timestamp DESC
                LIMIT 1
            ), 0) AS ventilation_power,
            p.occupancy
        FROM pivoted p
        WHERE p.co2 IS NOT NULL
          AND p.temperature IS NOT NULL
          AND p.humidity IS NOT NULL
        ORDER BY p.timestamp
    """
    with connect() as conn:
        return pd.read_sql(query, conn)


def add_time_features(df: pd.DataFrame) -> pd.DataFrame:
    df = df.copy()
    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    df["hour"] = df["timestamp"].dt.hour
    df["day_of_week"] = df["timestamp"].dt.dayofweek
    return df


def build_targets(df: pd.DataFrame) -> pd.DataFrame:
    prepared = []
    for _room_id, group in df.groupby("room_id"):
        group = group.sort_values("timestamp").reset_index(drop=True)
        result = group.copy()
        lookup = group[["timestamp", "co2", "temperature", "humidity"]].copy()

        for horizon in HORIZONS:
            future = lookup.copy()
            future["source_timestamp"] = future["timestamp"] - pd.Timedelta(minutes=horizon)
            future = future.rename(
                columns={
                    "co2": f"co2_{horizon}",
                    "temperature": f"temperature_{horizon}",
                    "humidity": f"humidity_{horizon}",
                }
            )
            matched = pd.merge_asof(
                result[["timestamp"]].sort_values("timestamp"),
                future.sort_values("source_timestamp"),
                left_on="timestamp",
                right_on="source_timestamp",
                direction="nearest",
                tolerance=pd.Timedelta(seconds=90),
            )
            for metric in ("co2", "temperature", "humidity"):
                result[f"{metric}_{horizon}"] = matched[f"{metric}_{horizon}"].to_numpy()

        prepared.append(result)

    dataset = pd.concat(prepared, ignore_index=True) if prepared else pd.DataFrame()
    return dataset.dropna(subset=TARGET_COLUMNS)


def calculate_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> dict:
    metrics: dict[str, dict[str, float]] = {}
    for metric in ("co2", "temperature", "humidity"):
        indices = [TARGET_COLUMNS.index(f"{metric}_{horizon}") for horizon in HORIZONS]
        metric_true = y_true[:, indices]
        metric_pred = y_pred[:, indices]
        metrics[metric] = {
            "mae": round(float(mean_absolute_error(metric_true, metric_pred)), 4),
            "rmse": round(float(mean_squared_error(metric_true, metric_pred, squared=False)), 4),
        }

    for horizon in HORIZONS:
        indices = [TARGET_COLUMNS.index(f"{metric}_{horizon}") for metric in ("co2", "temperature", "humidity")]
        metrics[f"horizon_{horizon}_minutes"] = {
            "mae": round(float(mean_absolute_error(y_true[:, indices], y_pred[:, indices])), 4),
            "rmse": round(float(mean_squared_error(y_true[:, indices], y_pred[:, indices], squared=False)), 4),
        }
    return metrics


def save_model_version(version: str, metrics: dict, trained_from, trained_to) -> None:
    with connect() as conn:
        with conn.cursor() as cur:
            cur.execute("UPDATE ai_model_versions SET active = FALSE WHERE active = TRUE")
            cur.execute(
                """
                INSERT INTO ai_model_versions(version, model_path, metrics, trained_from, trained_to, active)
                VALUES (%s, %s, %s, %s, %s, TRUE)
                ON CONFLICT (version) DO UPDATE SET
                    model_path = EXCLUDED.model_path,
                    metrics = EXCLUDED.metrics,
                    trained_from = EXCLUDED.trained_from,
                    trained_to = EXCLUDED.trained_to,
                    active = TRUE
                """,
                (version, str(model_path()), Jsonb(metrics), trained_from, trained_to),
            )


def train() -> dict:
    ensure_schema()
    raw = load_telemetry()
    if len(raw) < 80:
        artifact = save_heuristic_artifact(f"Need at least 80 telemetry rows from sensor_data, got {len(raw)}")
        save_model_version(artifact["version"], artifact["metrics"], None, None)
        logger.warning("Not enough sensor_data rows for training: %s", len(raw))
        return artifact

    df = add_time_features(raw)
    dataset = build_targets(df)
    if len(dataset) < 40:
        artifact = save_heuristic_artifact(f"Need at least 40 supervised rows, got {len(dataset)}")
        save_model_version(artifact["version"], artifact["metrics"], None, None)
        logger.warning("Not enough supervised rows for training: %s", len(dataset))
        return artifact

    dataset = dataset.sort_values("timestamp").reset_index(drop=True)
    split_index = max(1, int(len(dataset) * 0.8))
    train_df = dataset.iloc[:split_index]
    validation_df = dataset.iloc[split_index:]
    if validation_df.empty:
        validation_df = dataset.tail(max(1, len(dataset) // 5))
        train_df = dataset.iloc[: -len(validation_df)]

    x_train = train_df[FEATURE_COLUMNS].to_numpy(dtype=float)
    y_train = train_df[TARGET_COLUMNS].to_numpy(dtype=float)
    x_validation = validation_df[FEATURE_COLUMNS].to_numpy(dtype=float)
    y_validation = validation_df[TARGET_COLUMNS].to_numpy(dtype=float)

    model = RandomForestRegressor(
        n_estimators=120,
        min_samples_leaf=2,
        random_state=42,
        n_jobs=-1,
    )
    model.fit(x_train, y_train)
    y_pred = model.predict(x_validation)
    metrics = calculate_metrics(y_validation, y_pred)
    metrics["rows"] = {
        "raw": int(len(raw)),
        "supervised": int(len(dataset)),
        "train": int(len(train_df)),
        "validation": int(len(validation_df)),
    }

    version = f"rf-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"
    artifact = {
        "kind": "sklearn",
        "version": version,
        "model": model,
        "features": FEATURE_COLUMNS,
        "targets": TARGET_COLUMNS,
        "metrics": metrics,
        "created_at": datetime.now(timezone.utc).isoformat(),
    }

    path = model_path()
    path.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(artifact, path)
    save_model_version(version, metrics, dataset["timestamp"].min().to_pydatetime(), dataset["timestamp"].max().to_pydatetime())
    logger.info("Saved model %s to %s with metrics %s", version, path, json.dumps(metrics))
    return artifact


if __name__ == "__main__":
    train()
