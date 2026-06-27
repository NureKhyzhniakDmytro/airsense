package org.yooud.airsense.models

import java.util.Locale
import kotlin.math.abs

enum class MetricSeverity {
    NORMAL,
    WARNING,
    CRITICAL,
    UNKNOWN
}

data class MetricUiState(
    val label: String,
    val valueText: String,
    val severity: MetricSeverity,
    val progress: Float
)

fun parameterLabel(name: String): String = when (name.lowercase(Locale.US)) {
    "temperature" -> "Temperature"
    "co2" -> "CO2"
    "humidity" -> "Humidity"
    "device_speed" -> "Ventilation"
    else -> name.replace('_', ' ').replaceFirstChar { it.uppercaseChar() }
}

fun formatParameterValue(parameter: Parameter): String =
    parameter.value?.let { value -> "${formatMetricNumber(value)} ${parameter.unit}".trim() } ?: "No live value"

fun formatFanSpeed(value: Double?): String =
    value?.let { "${it.toInt().coerceIn(0, 100)}%" } ?: "Offline"

fun parameterUiState(parameter: Parameter): MetricUiState {
    val value = parameter.value
    return MetricUiState(
        label = parameterLabel(parameter.name),
        valueText = formatParameterValue(parameter),
        severity = parameterSeverity(parameter),
        progress = if (value == null) {
            0f
        } else {
            ((value - parameter.minValue) / (parameter.maxValue - parameter.minValue).coerceAtLeast(1.0))
                .toFloat()
                .coerceIn(0f, 1f)
        }
    )
}

fun parameterSeverity(parameter: Parameter): MetricSeverity {
    val value = parameter.value ?: return MetricSeverity.UNKNOWN
    return when (parameter.name.lowercase(Locale.US)) {
        "temperature" -> when {
            value < 15.0 || value > 32.0 -> MetricSeverity.CRITICAL
            value < 18.0 || value > 27.0 -> MetricSeverity.WARNING
            else -> MetricSeverity.NORMAL
        }
        "co2" -> when {
            value >= 1400.0 -> MetricSeverity.CRITICAL
            value >= 1000.0 -> MetricSeverity.WARNING
            else -> MetricSeverity.NORMAL
        }
        "humidity" -> when {
            value < 20.0 || value > 70.0 -> MetricSeverity.CRITICAL
            value < 30.0 || value > 60.0 -> MetricSeverity.WARNING
            else -> MetricSeverity.NORMAL
        }
        else -> {
            val minDistance = abs(value - parameter.minValue)
            val maxDistance = abs(parameter.maxValue - value)
            val range = (parameter.maxValue - parameter.minValue).coerceAtLeast(1.0)
            when {
                minDistance / range <= 0.05 || maxDistance / range <= 0.05 -> MetricSeverity.CRITICAL
                minDistance / range <= 0.15 || maxDistance / range <= 0.15 -> MetricSeverity.WARNING
                else -> MetricSeverity.NORMAL
            }
        }
    }
}

fun roomSeverity(room: Room): MetricSeverity {
    val severities = room.parameters.orEmpty().map(::parameterSeverity)
    return when {
        MetricSeverity.CRITICAL in severities -> MetricSeverity.CRITICAL
        MetricSeverity.WARNING in severities -> MetricSeverity.WARNING
        severities.isEmpty() || severities.all { it == MetricSeverity.UNKNOWN } -> MetricSeverity.UNKNOWN
        else -> MetricSeverity.NORMAL
    }
}

fun severityLabel(severity: MetricSeverity): String = when (severity) {
    MetricSeverity.NORMAL -> "Normal"
    MetricSeverity.WARNING -> "Warning"
    MetricSeverity.CRITICAL -> "Critical"
    MetricSeverity.UNKNOWN -> "No live data"
}

private fun formatMetricNumber(value: Double): String =
    if (abs(value - value.toInt()) < 0.05) {
        value.toInt().toString()
    } else {
        String.format(Locale.US, "%.1f", value)
    }
