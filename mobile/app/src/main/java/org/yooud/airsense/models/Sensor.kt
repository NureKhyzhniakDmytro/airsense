package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class Sensor(
    val id: Int,
    @SerializedName("type_name")
    val typeName: String,
    @SerializedName("serial_number")
    val serialNumber: String,
    val types: List<String>,
    val parameters: List<Parameter>?
)

