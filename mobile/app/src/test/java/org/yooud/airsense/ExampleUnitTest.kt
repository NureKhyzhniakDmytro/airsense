package org.yooud.airsense

import org.junit.Assert.*
import org.junit.Test
import org.yooud.airsense.models.MetricSeverity
import org.yooud.airsense.models.Parameter
import org.yooud.airsense.models.canManageRole
import org.yooud.airsense.models.formatFanSpeed
import org.yooud.airsense.models.formatParameterValue
import org.yooud.airsense.models.isReadOnlyRole
import org.yooud.airsense.models.parameterSeverity

class ExampleUnitTest {
    @Test
    fun userRoleIsReadOnly() {
        assertTrue(isReadOnlyRole("user"))
        assertFalse(canManageRole("user"))
    }

    @Test
    fun ownerAndAdminCanManage() {
        assertFalse(isReadOnlyRole("owner"))
        assertTrue(canManageRole("owner"))
        assertFalse(isReadOnlyRole("admin"))
        assertTrue(canManageRole("admin"))
    }

    @Test
    fun co2SeverityUsesAirQualityThresholds() {
        assertEquals(
            MetricSeverity.NORMAL,
            parameterSeverity(Parameter("co2", 850.0, "ppm", 350.0, 2000.0))
        )
        assertEquals(
            MetricSeverity.WARNING,
            parameterSeverity(Parameter("co2", 1100.0, "ppm", 350.0, 2000.0))
        )
        assertEquals(
            MetricSeverity.CRITICAL,
            parameterSeverity(Parameter("co2", 1500.0, "ppm", 350.0, 2000.0))
        )
    }

    @Test
    fun temperatureSeverityFlagsColdAndHotRooms() {
        assertEquals(
            MetricSeverity.WARNING,
            parameterSeverity(Parameter("temperature", 17.5, "°C", 10.0, 40.0))
        )
        assertEquals(
            MetricSeverity.CRITICAL,
            parameterSeverity(Parameter("temperature", 33.0, "°C", 10.0, 40.0))
        )
    }

    @Test
    fun metricFormattingIsCompact() {
        assertEquals("24.2 °C", formatParameterValue(Parameter("temperature", 24.2, "°C", 10.0, 40.0)))
        assertEquals("410 ppm", formatParameterValue(Parameter("co2", 410.0, "ppm", 350.0, 2000.0)))
        assertEquals("42%", formatFanSpeed(42.7))
        assertEquals("Offline", formatFanSpeed(null))
    }
}
