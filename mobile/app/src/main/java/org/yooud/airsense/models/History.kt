package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class HistoryPoint(
    val value: Double,
    val timestamp: Long
)

data class HistoryDevice(
    val id: Int,
    @SerializedName("type_name")
    val typeName: String?,
    @SerializedName("serial_number")
    val serialNumber: String?,
    val history: List<HistoryPoint>
)

data class HistoryMetadata(
    val from: Long,
    val to: Long,
    val interval: String
)

data class DeviceHistoryResponse(
    val data: List<HistoryDevice>,
    val metadata: HistoryMetadata
)
