from __future__ import annotations

from datetime import datetime, timezone

import numpy as np

from app.model_store import ModelBundle, feature_row, heuristic_predictions, predict
from app.schemas import TelemetrySample


def sample(**overrides) -> TelemetrySample:
    data = {
        "room_id": 7,
        "timestamp": datetime(2026, 6, 21, 8, 30),
        "co2": 1200,
        "temperature": 27,
        "humidity": 55,
        "ventilation_power": 25,
    }
    data.update(overrides)
    return TelemetrySample(**data)


def test_feature_row_uses_supply_exhaust_fallback_and_utc_time_features() -> None:
    row = feature_row(sample())

    assert row == [1200, 27, 55, 25, 25, 25, 8, 6, 7]


def test_feature_row_uses_explicit_supply_and_exhaust_power() -> None:
    row = feature_row(
        sample(
            timestamp=datetime(2026, 6, 22, 1, 15, tzinfo=timezone.utc),
            ventilation_power=30,
            supply_ventilation_power=70,
            exhaust_ventilation_power=20,
        )
    )

    assert row == [1200, 27, 55, 30, 70, 20, 1, 0, 7]


def test_heuristic_prediction_reduces_excess_co2_with_more_ventilation() -> None:
    low_ventilation = heuristic_predictions(
        sample(co2=1600, ventilation_power=0, supply_ventilation_power=0, exhaust_ventilation_power=0),
        [30],
    )[0]
    high_ventilation = heuristic_predictions(
        sample(co2=1600, ventilation_power=90, supply_ventilation_power=90, exhaust_ventilation_power=90),
        [30],
    )[0]

    assert high_ventilation.co2 < low_ventilation.co2
    assert high_ventilation.temperature < low_ventilation.temperature
    assert high_ventilation.humidity < low_ventilation.humidity


def test_predict_filters_to_supported_horizons_and_uses_trained_model() -> None:
    class FakeModel:
        def predict(self, _features):
            return np.array([[810, 22.5, 44.2, 790, 22.1, 43.8, 770, 21.9, 43.1]])

    bundle = ModelBundle(version="test-model", mode="trained", model=FakeModel(), metrics={})

    points = predict(bundle, sample(), [5, 20, 45])

    assert len(points) == 1
    assert points[0].horizon_minutes == 20
    assert points[0].co2 == 790
    assert points[0].temperature == 22.1
    assert points[0].humidity == 43.8

