package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class RegisterRequest(
    @SerializedName("notification_token")
    val notificationToken: String?
)
