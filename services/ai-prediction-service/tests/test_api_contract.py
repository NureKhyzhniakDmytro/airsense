from __future__ import annotations

from datetime import datetime, timezone

from app import main
from app.model_store import ModelBundle
from app.schemas import (
    PredictRequest,
    RecommendationRequest,
    SimulateRequest,
    TelemetrySample,
    VentilationScenario,
)


def sample(**overrides) -> TelemetrySample:
    data = {
        "room_id": 2,
        "timestamp": datetime(2026, 6, 21, 12, 0, tzinfo=timezone.utc),
        "co2": 1450,
        "temperature": 25.5,
        "humidity": 52,
        "ventilation_power": 10,
        "supply_ventilation_power": 10,
        "exhaust_ventilation_power": 10,
    }
    data.update(overrides)
    return TelemetrySample(**data)


def use_heuristic_model(monkeypatch) -> None:
    monkeypatch.setattr(
        main,
        "load_model",
        lambda: ModelBundle(version="heuristic-test", mode="heuristic", model=None, metrics={}),
    )


def test_predict_endpoint_returns_requested_supported_horizons(monkeypatch) -> None:
    use_heuristic_model(monkeypatch)

    response = main.predict_endpoint(PredictRequest(sample=sample(), horizons_minutes=[10, 30]))

    assert response.model_version == "heuristic-test"
    assert response.mode == "heuristic"
    assert [point.horizon_minutes for point in response.predictions] == [10, 30]


def test_simulate_endpoint_applies_each_ventilation_scenario(monkeypatch) -> None:
    use_heuristic_model(monkeypatch)

    response = main.simulate_endpoint(
        SimulateRequest(
            sample=sample(co2=1600),
            scenarios=[
                VentilationScenario(label="off", ventilation_power=0),
                VentilationScenario(label="boost", ventilation_power=80),
            ],
            horizons_minutes=[30],
        )
    )

    assert [scenario.label for scenario in response.scenarios] == ["off", "boost"]
    assert response.scenarios[1].predictions[0].co2 < response.scenarios[0].predictions[0].co2


def test_recommendation_endpoint_respects_max_power_and_returns_non_commanding_advice(monkeypatch) -> None:
    use_heuristic_model(monkeypatch)

    response = main.recommendation_endpoint(
        RecommendationRequest(
            sample=sample(co2=1600),
            target_co2=800,
            max_ventilation_power=40,
            horizon_minutes=20,
        )
    )

    assert response.suggested_ventilation_power == 40
    assert response.sends_command is False
    assert response.predicted.horizon_minutes == 20
    assert "ventilation power 40%" in response.reason

