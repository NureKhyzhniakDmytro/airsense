from __future__ import annotations

from datetime import datetime, timezone
from typing import Literal

from pydantic import BaseModel, Field


class TelemetrySample(BaseModel):
    room_id: int = Field(gt=0)
    timestamp: datetime | None = None
    co2: float = Field(gt=250, lt=10000)
    temperature: float = Field(gt=-40, lt=80)
    humidity: float = Field(ge=0, le=100)
    ventilation_power: float = Field(ge=0, le=100)
    occupancy: int = Field(ge=0)

    def effective_timestamp(self) -> datetime:
        if self.timestamp is None:
            return datetime.now(timezone.utc)
        if self.timestamp.tzinfo is None:
            return self.timestamp.replace(tzinfo=timezone.utc)
        return self.timestamp.astimezone(timezone.utc)


class PredictRequest(BaseModel):
    sample: TelemetrySample
    horizons_minutes: list[int] = Field(default_factory=lambda: [10, 20, 30])


class PredictionPoint(BaseModel):
    horizon_minutes: int
    co2: float
    temperature: float
    humidity: float


class PredictResponse(BaseModel):
    model_version: str
    mode: Literal["trained", "heuristic"]
    predictions: list[PredictionPoint]


class VentilationScenario(BaseModel):
    label: str
    ventilation_power: float = Field(ge=0, le=100)


class SimulateRequest(BaseModel):
    sample: TelemetrySample
    scenarios: list[VentilationScenario]
    horizons_minutes: list[int] = Field(default_factory=lambda: [10, 20, 30])


class ScenarioSimulation(BaseModel):
    label: str
    ventilation_power: float
    predictions: list[PredictionPoint]


class SimulateResponse(BaseModel):
    model_version: str
    mode: Literal["trained", "heuristic"]
    scenarios: list[ScenarioSimulation]


class RecommendationRequest(BaseModel):
    sample: TelemetrySample
    target_co2: float = Field(default=900, ge=400, le=2500)
    max_ventilation_power: float = Field(default=100, ge=0, le=100)
    horizon_minutes: int = Field(default=20, ge=5, le=60)


class RecommendationResponse(BaseModel):
    model_version: str
    mode: Literal["trained", "heuristic"]
    suggested_ventilation_power: float
    reason: str
    predicted: PredictionPoint
    mqtt_command_topic: str
    sends_command: bool = False
