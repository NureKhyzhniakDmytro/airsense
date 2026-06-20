from __future__ import annotations

import math
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

import joblib
import numpy as np

from .schemas import PredictionPoint, TelemetrySample


FEATURE_COLUMNS = [
    "co2",
    "temperature",
    "humidity",
    "ventilation_power",
    "supply_ventilation_power",
    "exhaust_ventilation_power",
    "hour",
    "day_of_week",
    "room_id",
]
HORIZONS = [10, 20, 30]
TARGET_COLUMNS = [
    f"{metric}_{horizon}"
    for horizon in HORIZONS
    for metric in ("co2", "temperature", "humidity")
]


@dataclass
class ModelBundle:
    version: str
    mode: str
    model: object | None
    metrics: dict


def model_path() -> Path:
    return Path(os.getenv("MODEL_PATH", "/models/model.joblib"))


def load_model() -> ModelBundle:
    path = model_path()
    if not path.exists():
        return ModelBundle(version="heuristic-untrained", mode="heuristic", model=None, metrics={})

    artifact = joblib.load(path)
    if artifact.get("kind") != "sklearn":
        return ModelBundle(
            version=artifact.get("version", "heuristic-untrained"),
            mode="heuristic",
            model=None,
            metrics=artifact.get("metrics", {}),
        )

    return ModelBundle(
        version=artifact.get("version", "unknown"),
        mode="trained",
        model=artifact["model"],
        metrics=artifact.get("metrics", {}),
    )


def feature_row(sample: TelemetrySample) -> list[float]:
    timestamp = sample.effective_timestamp()
    return [
        sample.co2,
        sample.temperature,
        sample.humidity,
        sample.ventilation_power,
        sample.effective_supply_ventilation_power(),
        sample.effective_exhaust_ventilation_power(),
        timestamp.hour,
        timestamp.weekday(),
        sample.room_id,
    ]


def clamp(value: float, lower: float, upper: float) -> float:
    if math.isnan(value) or math.isinf(value):
        return lower
    return max(lower, min(upper, value))


def heuristic_predictions(sample: TelemetrySample, horizons: Iterable[int]) -> list[PredictionPoint]:
    points: list[PredictionPoint] = []
    supply_power = sample.effective_supply_ventilation_power()
    exhaust_power = sample.effective_exhaust_ventilation_power()
    exchange_power = max(sample.ventilation_power, (supply_power + exhaust_power) / 2)
    balance_factor = 1 - abs(supply_power - exhaust_power) / 180
    for horizon in horizons:
        scale = horizon / 10.0
        co2 = sample.co2 - exchange_power * (6.2 + balance_factor * 1.1) * scale
        co2 -= max(sample.co2 - 420.0, 0.0) * 0.03 * scale

        temperature = sample.temperature - (supply_power * 0.013 + exhaust_power * 0.005) * scale
        humidity = sample.humidity - (supply_power * 0.026 + exhaust_power * 0.011) * scale

        points.append(
            PredictionPoint(
                horizon_minutes=horizon,
                co2=round(clamp(co2, 400, 5000), 2),
                temperature=round(clamp(temperature, 10, 45), 2),
                humidity=round(clamp(humidity, 0, 100), 2),
            )
        )
    return points


def trained_predictions(bundle: ModelBundle, sample: TelemetrySample, horizons: Iterable[int]) -> list[PredictionPoint]:
    if bundle.model is None:
        return heuristic_predictions(sample, horizons)

    try:
        prediction = bundle.model.predict(np.array([feature_row(sample)], dtype=float))[0]
    except ValueError:
        return heuristic_predictions(sample, horizons)
    by_target = dict(zip(TARGET_COLUMNS, prediction, strict=False))
    requested = set(horizons)
    points = []
    for horizon in HORIZONS:
        if horizon not in requested:
            continue
        points.append(
            PredictionPoint(
                horizon_minutes=horizon,
                co2=round(clamp(float(by_target[f"co2_{horizon}"]), 400, 5000), 2),
                temperature=round(clamp(float(by_target[f"temperature_{horizon}"]), 10, 45), 2),
                humidity=round(clamp(float(by_target[f"humidity_{horizon}"]), 0, 100), 2),
            )
        )
    return points


def predict(bundle: ModelBundle, sample: TelemetrySample, horizons: Iterable[int]) -> list[PredictionPoint]:
    filtered = [h for h in horizons if h in HORIZONS]
    if not filtered:
        filtered = HORIZONS
    if bundle.mode == "trained":
        return trained_predictions(bundle, sample, filtered)
    return heuristic_predictions(sample, filtered)


def save_heuristic_artifact(reason: str) -> dict:
    path = model_path()
    path.parent.mkdir(parents=True, exist_ok=True)
    artifact = {
        "kind": "heuristic",
        "version": f"heuristic-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}",
        "metrics": {"status": "insufficient_data", "reason": reason},
        "created_at": datetime.now(timezone.utc).isoformat(),
    }
    joblib.dump(artifact, path)
    return artifact
