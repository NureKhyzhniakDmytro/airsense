package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class Parameter(
    val name: String,
    val value: Double?,
    val unit: String,
    @SerializedName("min_value")
    val minValue: Double,
    @SerializedName("max_value")
    val maxValue: Double
)
