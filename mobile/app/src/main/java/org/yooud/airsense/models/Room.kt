package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class Room(
    val id: Int,
    val name: String,
    val icon: String?,
    val parameters: List<Parameter>?,
    @SerializedName("device_speed")
    val deviceSpeed: Double?
)
