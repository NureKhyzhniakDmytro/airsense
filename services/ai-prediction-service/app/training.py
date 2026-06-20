from __future__ import annotations

import json
import logging
import os
from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from psycopg.types.json import Jsonb
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.metrics import mean_absolute_error, mean_squared_error
from sklearn.multioutput import MultiOutputRegressor

from .db import connect, ensure_schema
from .model_store import FEATURE_COLUMNS, HORIZONS, TARGET_COLUMNS, model_path, save_heuristic_artifact


logging.basicConfig(level="INFO", format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger("ai-training-job")

TELEMETRY_COLUMNS = [
    "source",
    "series_id",
    "room_id",
    "timestamp",
    "co2",
    "temperature",
    "humidity",
    "ventilation_power",
    "supply_ventilation_power",
    "exhaust_ventilation_power",
]
REQUIRED_EXTERNAL_COLUMNS = {"timestamp", "co2", "temperature", "humidity"}


def external_dataset_dir() -> Path:
    configured = os.getenv("EXTERNAL_DATASET_DIR")
    if configured:
        return Path(configured)
    return Path(__file__).resolve().parents[1] / "datasets" / "normalized"


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
              AND p.name IN ('co2', 'temperature', 'humidity')
            GROUP BY s.room_id, date_trunc('minute', sd.timestamp), p.name
        ),
        pivoted AS (
            SELECT
                room_id,
                timestamp,
                AVG(value) FILTER (WHERE parameter = 'co2') AS co2,
                AVG(value) FILTER (WHERE parameter = 'temperature') AS temperature,
                AVG(value) FILTER (WHERE parameter = 'humidity') AS humidity
            FROM sensor_points
            GROUP BY room_id, timestamp
        ),
        vent_roles AS (
            SELECT
                r.id AS room_id,
                (item ->> 'device_id')::int AS device_id,
                CASE
                    WHEN lower(item ->> 'airflow_role') IN ('supply', 'exhaust') THEN lower(item ->> 'airflow_role')
                    WHEN lower(COALESCE(item ->> 'label', '')) ~ '(extract|exhaust|return|outlet)' THEN 'exhaust'
                    ELSE 'supply'
                END AS airflow_role
            FROM rooms r
            CROSS JOIN LATERAL jsonb_array_elements(
                CASE
                    WHEN jsonb_typeof(r.layout -> 'items') = 'array' THEN r.layout -> 'items'
                    ELSE '[]'::jsonb
                END
            ) AS item
            WHERE lower(item ->> 'type') = 'vent'
              AND (item ->> 'device_id') ~ '^[0-9]+$'
        ),
        room_devices AS (
            SELECT
                d.room_id,
                d.id AS device_id,
                COALESCE(
                    vr.airflow_role,
                    CASE
                        WHEN row_number() OVER (PARTITION BY d.room_id ORDER BY d.id) % 2 = 0 THEN 'exhaust'
                        ELSE 'supply'
                    END
                ) AS airflow_role
            FROM devices d
            LEFT JOIN vent_roles vr ON vr.room_id = d.room_id AND vr.device_id = d.id
            WHERE d.room_id IS NOT NULL
        ),
        device_points AS (
            SELECT
                rd.room_id,
                date_trunc('minute', dd.timestamp) AS timestamp,
                rd.airflow_role,
                AVG(dd.value) AS value
            FROM device_data dd
            JOIN room_devices rd ON rd.device_id = dd.device_id
            GROUP BY rd.room_id, date_trunc('minute', dd.timestamp), rd.airflow_role
        ),
        device_by_minute AS (
            SELECT
                room_id,
                timestamp,
                AVG(value) AS ventilation_power,
                AVG(value) FILTER (WHERE airflow_role = 'supply') AS supply_ventilation_power,
                AVG(value) FILTER (WHERE airflow_role = 'exhaust') AS exhaust_ventilation_power
            FROM device_points
            GROUP BY room_id, timestamp
        ),
        with_device AS (
            SELECT
                p.room_id,
                p.timestamp,
                p.co2,
                p.temperature,
                p.humidity,
                COALESCE(d.ventilation_power, 0) AS ventilation_power,
                COALESCE(d.supply_ventilation_power, 0) AS supply_ventilation_power,
                COALESCE(d.exhaust_ventilation_power, 0) AS exhaust_ventilation_power
            FROM pivoted p
            LEFT JOIN device_by_minute d ON d.room_id = p.room_id AND d.timestamp = p.timestamp
            WHERE p.co2 IS NOT NULL
              AND p.temperature IS NOT NULL
              AND p.humidity IS NOT NULL
        )
        SELECT
            room_id,
            timestamp,
            co2,
            temperature,
            humidity,
            ventilation_power,
            supply_ventilation_power,
            exhaust_ventilation_power
        FROM with_device
        ORDER BY timestamp
    """
    with connect() as conn:
        df = pd.read_sql(query, conn)

    if df.empty:
        return pd.DataFrame(columns=TELEMETRY_COLUMNS)

    df = df.copy()
    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    df["source"] = "airsense_db"
    df["series_id"] = "airsense_db:room:" + df["room_id"].astype(str)
    return df[TELEMETRY_COLUMNS]


def normalize_external_frame(df: pd.DataFrame, path: Path) -> pd.DataFrame:
    missing = REQUIRED_EXTERNAL_COLUMNS.difference(df.columns)
    if missing:
        raise ValueError(f"{path} is missing required columns: {', '.join(sorted(missing))}")

    normalized = df.copy()
    normalized["timestamp"] = pd.to_datetime(normalized["timestamp"], utc=True, errors="coerce")
    normalized["source"] = normalized.get("source", path.stem)
    if "room_id" not in normalized.columns:
        normalized["room_id"] = 1
    if "series_id" not in normalized.columns:
        normalized["series_id"] = normalized["source"].astype(str) + ":room:" + normalized["room_id"].astype(str)

    for column in (
        "co2",
        "temperature",
        "humidity",
        "ventilation_power",
        "supply_ventilation_power",
        "exhaust_ventilation_power",
        "room_id",
    ):
        if column not in normalized.columns:
            normalized[column] = 0.0
        normalized[column] = pd.to_numeric(normalized[column], errors="coerce")

    normalized["ventilation_power"] = normalized["ventilation_power"].fillna(
        (normalized["supply_ventilation_power"].fillna(0) + normalized["exhaust_ventilation_power"].fillna(0)) / 2
    )
    normalized["supply_ventilation_power"] = normalized["supply_ventilation_power"].fillna(normalized["ventilation_power"])
    normalized["exhaust_ventilation_power"] = normalized["exhaust_ventilation_power"].fillna(normalized["ventilation_power"])

    normalized = normalized.dropna(
        subset=[
            "timestamp",
            "room_id",
            "co2",
            "temperature",
            "humidity",
            "ventilation_power",
            "supply_ventilation_power",
            "exhaust_ventilation_power",
        ]
    )
    normalized = normalized[
        normalized["co2"].between(300, 5000)
        & normalized["temperature"].between(-20, 60)
        & normalized["humidity"].between(0, 100)
        & normalized["ventilation_power"].between(0, 100)
        & normalized["supply_ventilation_power"].between(0, 100)
        & normalized["exhaust_ventilation_power"].between(0, 100)
        & (normalized["room_id"] > 0)
    ]
    normalized["room_id"] = normalized["room_id"].round().astype(int)
    return normalized[TELEMETRY_COLUMNS].sort_values(["series_id", "timestamp"]).reset_index(drop=True)


def load_external_telemetry() -> pd.DataFrame:
    directory = external_dataset_dir()
    if not directory.exists():
        logger.info("External dataset directory does not exist: %s", directory)
        return pd.DataFrame(columns=TELEMETRY_COLUMNS)

    merged_path = directory / "airsense_external_training.csv"
    paths = [merged_path] if merged_path.exists() else sorted(directory.glob("*.csv"))
    frames: list[pd.DataFrame] = []
    for path in paths:
        if not path.is_file():
            continue
        try:
            frames.append(normalize_external_frame(pd.read_csv(path), path))
        except Exception:
            logger.exception("Unable to load external training dataset %s", path)
            raise

    if not frames:
        return pd.DataFrame(columns=TELEMETRY_COLUMNS)

    external = pd.concat(frames, ignore_index=True)
    logger.info(
        "Loaded %s external telemetry rows from %s (%s series)",
        len(external),
        directory,
        external["series_id"].nunique(),
    )
    return external[TELEMETRY_COLUMNS]


def combine_training_sources(db_raw: pd.DataFrame, external_raw: pd.DataFrame) -> pd.DataFrame:
    frames = [frame for frame in (db_raw, external_raw) if not frame.empty]
    if not frames:
        return pd.DataFrame(columns=TELEMETRY_COLUMNS)
    raw = pd.concat(frames, ignore_index=True)
    return raw[TELEMETRY_COLUMNS].sort_values(["series_id", "timestamp"]).reset_index(drop=True)


def row_counts_by_source(df: pd.DataFrame) -> dict[str, int]:
    if df.empty or "source" not in df.columns:
        return {}
    return {str(source): int(count) for source, count in df.groupby("source").size().items()}


def add_time_features(df: pd.DataFrame) -> pd.DataFrame:
    df = df.copy()
    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    df["hour"] = df["timestamp"].dt.hour
    df["day_of_week"] = df["timestamp"].dt.dayofweek
    return df


def build_targets(df: pd.DataFrame) -> pd.DataFrame:
    prepared = []
    group_column = "series_id" if "series_id" in df.columns else "room_id"
    for _series_id, group in df.groupby(group_column):
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


def split_supervised_dataset(dataset: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
    train_parts: list[pd.DataFrame] = []
    validation_parts: list[pd.DataFrame] = []
    group_column = "series_id" if "series_id" in dataset.columns else "room_id"

    for _series_id, group in dataset.groupby(group_column, sort=False):
        group = group.sort_values("timestamp").reset_index(drop=True)
        if len(group) < 5:
            train_parts.append(group)
            continue

        split_index = min(len(group) - 1, max(1, int(len(group) * 0.8)))
        train_parts.append(group.iloc[:split_index])
        validation_parts.append(group.iloc[split_index:])

    train_df = pd.concat(train_parts, ignore_index=True) if train_parts else pd.DataFrame(columns=dataset.columns)
    validation_df = (
        pd.concat(validation_parts, ignore_index=True)
        if validation_parts
        else pd.DataFrame(columns=dataset.columns)
    )

    if validation_df.empty and not train_df.empty:
        validation_size = max(1, len(train_df) // 5)
        validation_df = train_df.tail(validation_size).copy()
        train_df = train_df.iloc[:-validation_size].copy()

    return train_df, validation_df


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
    db_raw = load_telemetry()
    external_raw = load_external_telemetry()
    raw = combine_training_sources(db_raw, external_raw)
    if len(raw) < 80:
        artifact = save_heuristic_artifact(f"Need at least 80 telemetry rows from training sources, got {len(raw)}")
        save_model_version(artifact["version"], artifact["metrics"], None, None)
        logger.warning("Not enough telemetry rows for training: %s", len(raw))
        return artifact

    df = add_time_features(raw)
    dataset = build_targets(df)
    if len(dataset) < 40:
        artifact = save_heuristic_artifact(f"Need at least 40 supervised rows, got {len(dataset)}")
        save_model_version(artifact["version"], artifact["metrics"], None, None)
        logger.warning("Not enough supervised rows for training: %s", len(dataset))
        return artifact

    train_df, validation_df = split_supervised_dataset(dataset)

    x_train = train_df[FEATURE_COLUMNS].to_numpy(dtype=float)
    y_train = train_df[TARGET_COLUMNS].to_numpy(dtype=float)
    x_validation = validation_df[FEATURE_COLUMNS].to_numpy(dtype=float)
    y_validation = validation_df[TARGET_COLUMNS].to_numpy(dtype=float)

    model = MultiOutputRegressor(
        HistGradientBoostingRegressor(
            max_iter=260,
            learning_rate=0.045,
            max_leaf_nodes=31,
            min_samples_leaf=20,
            l2_regularization=0.01,
            monotonic_cst=[1, 1, 1, -1, -1, -1, 0, 0, 0],
            random_state=42,
        ),
        n_jobs=-1,
    )
    model.fit(x_train, y_train)
    y_pred = model.predict(x_validation)
    metrics = calculate_metrics(y_validation, y_pred)
    metrics["rows"] = {
        "raw": int(len(raw)),
        "db": int(len(db_raw)),
        "external": int(len(external_raw)),
        "supervised": int(len(dataset)),
        "train": int(len(train_df)),
        "validation": int(len(validation_df)),
        "series": int(raw["series_id"].nunique()) if "series_id" in raw.columns else int(raw["room_id"].nunique()),
    }
    metrics["sources"] = {
        "raw": row_counts_by_source(raw),
        "supervised": row_counts_by_source(dataset),
    }

    version = f"hgb-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"
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
