package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class RoomLayoutPoint(
    val x: Double,
    val y: Double
)

data class RoomLayoutGeometry(
    val type: String,
    val points: List<RoomLayoutPoint>
)

data class RoomLayoutItem(
    val id: String,
    val type: String,
    val label: String?,
    @SerializedName("sensor_id")
    val sensorId: Int?,
    @SerializedName("device_id")
    val deviceId: Int?,
    @SerializedName("serial_number")
    val serialNumber: String?,
    @SerializedName("airflow_role")
    val airflowRole: String?,
    @SerializedName("heat_load_kw")
    val heatLoadKw: Double?,
    @SerializedName("thermal_load")
    val thermalLoad: String?,
    val x: Double,
    val y: Double,
    val width: Double,
    val height: Double,
    val rotation: Double
)

data class RoomLayout(
    val width: Double,
    val height: Double,
    val unit: String,
    val geometry: RoomLayoutGeometry,
    val items: List<RoomLayoutItem>
)
