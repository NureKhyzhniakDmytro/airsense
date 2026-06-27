package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class Device(
    val id: Int,
    @SerializedName("serial_number")
    val serialNumber: String,
    @SerializedName("fan_speed")
    val fanSpeed: Double?,
    @SerializedName("active_at")
    val activeAt: Long?
)

