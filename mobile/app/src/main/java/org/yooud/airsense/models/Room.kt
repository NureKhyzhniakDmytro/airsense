package org.yooud.airsense.models

data class Room(
    val id: Int,
    val name: String,
    val parameters: List<Parameter>?,
    val device_speed: Int?
)
