package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class AiTelemetrySample(
    @SerializedName("room_id")
    val roomId: Int,
    val timestamp: String?,
    val co2: Double,
    val temperature: Double,
    val humidity: Double,
    @SerializedName("ventilation_power")
    val ventilationPower: Double,
    @SerializedName("supply_ventilation_power")
    val supplyVentilationPower: Double,
    @SerializedName("exhaust_ventilation_power")
    val exhaustVentilationPower: Double
)

data class AiPredictionPoint(
    @SerializedName("horizon_minutes")
    val horizonMinutes: Int,
    val co2: Double,
    val temperature: Double,
    val humidity: Double
)

data class AiPredictResponse(
    @SerializedName("model_version")
    val modelVersion: String,
    val mode: String,
    val predictions: List<AiPredictionPoint>
)

data class AiScenarioSimulation(
    val label: String,
    @SerializedName("ventilation_power")
    val ventilationPower: Double,
    val predictions: List<AiPredictionPoint>
)

data class AiSimulateResponse(
    @SerializedName("model_version")
    val modelVersion: String,
    val mode: String,
    val scenarios: List<AiScenarioSimulation>
)

data class AiControlSettings(
    @SerializedName("room_id")
    val roomId: Int,
    val enabled: Boolean,
    @SerializedName("target_co2")
    val targetCo2: Double,
    @SerializedName("target_temperature")
    val targetTemperature: Double?,
    @SerializedName("target_humidity")
    val targetHumidity: Double?,
    @SerializedName("max_ventilation_power")
    val maxVentilationPower: Double,
    @SerializedName("updated_at")
    val updatedAt: String?
)

data class AiRecommendationAudit(
    val id: Long,
    val timestamp: String?,
    @SerializedName("requested_power")
    val requestedPower: Double?,
    val status: String,
    @SerializedName("model_version")
    val modelVersion: String,
    val mode: String,
    val reason: String,
    val predicted: AiPredictionPoint?,
    val sample: AiTelemetrySample?
)

data class RoomAiInsights(
    @SerializedName("has_sample")
    val hasSample: Boolean,
    val message: String?,
    @SerializedName("generated_at")
    val generatedAt: String?,
    @SerializedName("telemetry_age_seconds")
    val telemetryAgeSeconds: Double?,
    val sample: AiTelemetrySample?,
    val prediction: AiPredictResponse?,
    val simulation: AiSimulateResponse?,
    @SerializedName("control_settings")
    val controlSettings: AiControlSettings?,
    @SerializedName("recent_recommendations")
    val recentRecommendations: List<AiRecommendationAudit>
)

