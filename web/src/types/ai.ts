export interface AiTelemetrySample {
  room_id: number;
  timestamp?: string | null;
  co2: number;
  temperature: number;
  humidity: number;
  ventilation_power: number;
}

export interface AiPredictionPoint {
  horizon_minutes: number;
  co2: number;
  temperature: number;
  humidity: number;
}

export interface AiPredictResponse {
  model_version: string;
  mode: "trained" | "heuristic" | string;
  predictions: AiPredictionPoint[];
}

export interface AiScenarioSimulation {
  label: string;
  ventilation_power: number;
  predictions: AiPredictionPoint[];
}

export interface AiSimulateResponse {
  model_version: string;
  mode: "trained" | "heuristic" | string;
  scenarios: AiScenarioSimulation[];
}

export interface AiRecommendationAudit {
  id: number;
  timestamp: string;
  requested_power?: number | null;
  status: "recommended" | "accepted" | "used" | string;
  model_version: string;
  mode: "trained" | "heuristic" | string;
  reason: string;
  predicted?: AiPredictionPoint | null;
  sample?: AiTelemetrySample | null;
}

export interface RoomAiInsights {
  has_sample: boolean;
  message?: string | null;
  generated_at: string;
  telemetry_age_seconds?: number | null;
  sample?: AiTelemetrySample | null;
  prediction?: AiPredictResponse | null;
  simulation?: AiSimulateResponse | null;
  recent_recommendations: AiRecommendationAudit[];
}
