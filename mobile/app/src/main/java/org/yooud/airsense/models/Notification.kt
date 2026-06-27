package org.yooud.airsense.models

import com.google.gson.annotations.SerializedName

data class UserNotification(
    val id: Long,
    val title: String,
    val body: String,
    val severity: String,
    val data: Map<String, String>?,
    @SerializedName("created_at")
    val createdAt: Long,
    @SerializedName("read_at")
    val readAt: Long?,
    @SerializedName("is_read")
    val isRead: Boolean
)

data class UnreadNotifications(
    @SerializedName("unread_count")
    val unreadCount: Int
)

