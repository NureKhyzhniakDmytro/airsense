package org.yooud.airsense.models

data class Parameter(
    val name: String,
    val value: Double,
    val unit: String,
    val min_value: Double,
    val max_value: Double
)
