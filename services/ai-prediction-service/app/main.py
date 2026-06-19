from __future__ import annotations

from fastapi import FastAPI

from .db import ensure_schema
from .model_store import HORIZONS, load_model, predict
from .schemas import (
    PredictRequest,
    PredictResponse,
    RecommendationRequest,
    RecommendationResponse,
    ScenarioSimulation,
    SimulateRequest,
    SimulateResponse,
    TelemetrySample,
)


app = FastAPI(title="AirSense AI Prediction Service", version="0.1.0")


@app.on_event("startup")
def startup() -> None:
    ensure_schema()


@app.get("/health")
def health() -> dict:
    bundle = load_model()
    return {"status": "ok", "model_version": bundle.version, "mode": bundle.mode}


@app.get("/model/version")
def model_version() -> dict:
    bundle = load_model()
    return {"version": bundle.version, "mode": bundle.mode, "metrics": bundle.metrics}


@app.post("/predict", response_model=PredictResponse)
def predict_endpoint(request: PredictRequest) -> PredictResponse:
    bundle = load_model()
    return PredictResponse(
        model_version=bundle.version,
        mode=bundle.mode,
        predictions=predict(bundle, request.sample, request.horizons_minutes),
    )


@app.post("/simulate", response_model=SimulateResponse)
def simulate_endpoint(request: SimulateRequest) -> SimulateResponse:
    bundle = load_model()
    scenarios = []
    for scenario in request.scenarios:
        sample = TelemetrySample(
            **request.sample.model_dump(exclude={"ventilation_power"}),
            ventilation_power=scenario.ventilation_power,
        )
        scenarios.append(
            ScenarioSimulation(
                label=scenario.label,
                ventilation_power=scenario.ventilation_power,
                predictions=predict(bundle, sample, request.horizons_minutes),
            )
        )

    return SimulateResponse(model_version=bundle.version, mode=bundle.mode, scenarios=scenarios)


@app.post("/recommendation", response_model=RecommendationResponse)
def recommendation_endpoint(request: RecommendationRequest) -> RecommendationResponse:
    bundle = load_model()
    horizon = min(HORIZONS, key=lambda value: abs(value - request.horizon_minutes))
    candidates = [0, 20, 40, 60, 80, min(100, request.max_ventilation_power)]
    candidates = sorted({power for power in candidates if power <= request.max_ventilation_power})
    best_power = candidates[-1]
    best_prediction = None

    for power in candidates:
        sample = TelemetrySample(
            **request.sample.model_dump(exclude={"ventilation_power"}),
            ventilation_power=power,
        )
        prediction = predict(bundle, sample, [horizon])[0]
        if best_prediction is None:
            best_prediction = prediction
        if prediction.co2 <= request.target_co2:
            best_power = power
            best_prediction = prediction
            break
        if prediction.co2 < best_prediction.co2:
            best_power = power
            best_prediction = prediction

    assert best_prediction is not None
    reason = (
        f"Expected CO2 is {best_prediction.co2} ppm in {horizon} minutes "
        f"with ventilation power {best_power}%."
    )

    return RecommendationResponse(
        model_version=bundle.version,
        mode=bundle.mode,
        suggested_ventilation_power=best_power,
        reason=reason,
        predicted=best_prediction,
        mqtt_command_topic="devices/{deviceId}/control",
        sends_command=False,
    )
