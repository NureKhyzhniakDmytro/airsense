package org.yooud.airsense.network

import org.yooud.airsense.models.AiControlSettings
import org.yooud.airsense.models.Device
import org.yooud.airsense.models.DeviceHistoryResponse
import org.yooud.airsense.models.Environment
import org.yooud.airsense.models.PaginationResponse
import org.yooud.airsense.models.RegisterRequest
import org.yooud.airsense.models.Room
import org.yooud.airsense.models.RoomAiInsights
import org.yooud.airsense.models.RoomLayout
import org.yooud.airsense.models.Sensor
import org.yooud.airsense.models.UnreadNotifications
import org.yooud.airsense.models.UserNotification
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface ApiService {
    @POST("auth")
    suspend fun register(@Body req: RegisterRequest): Response<Void>

    @GET("env")
    suspend fun getEnvironments(@Query("skip") skip: Int = 0, @Query("count") count: Int = 10): Response<PaginationResponse<Environment>>

    @GET("env/{id}")
    suspend fun getEnvironment(@Path("id") envId: Int): Response<Environment>

    @GET("env/{id}/room")
    suspend fun getRooms(@Path("id") envId: Int, @Query("skip") skip: Int = 0, @Query("count") count: Int = 10): Response<PaginationResponse<Room>>

    @GET("env/{envId}/room/{roomId}")
    suspend fun getRoom(@Path("envId") envId: Int, @Path("roomId") roomId: Int): Response<Room>

    @GET("env/{envId}/room/{roomId}/layout")
    suspend fun getRoomLayout(@Path("envId") envId: Int, @Path("roomId") roomId: Int): Response<RoomLayout>

    @GET("room/{roomId}/sensor")
    suspend fun getRoomSensors(@Path("roomId") roomId: Int, @Query("skip") skip: Int = 0, @Query("count") count: Int = 20): Response<PaginationResponse<Sensor>>

    @GET("room/{roomId}/device")
    suspend fun getRoomDevices(@Path("roomId") roomId: Int, @Query("skip") skip: Int = 0, @Query("count") count: Int = 20): Response<PaginationResponse<Device>>

    @GET("room/{roomId}/history")
    suspend fun getRoomDeviceHistory(
        @Path("roomId") roomId: Int,
        @Query("from") from: Long? = null,
        @Query("to") to: Long? = null,
        @Query("interval") interval: String = "hour"
    ): Response<DeviceHistoryResponse>

    @GET("room/{roomId}/{paramName}/history")
    suspend fun getRoomParameterHistory(
        @Path("roomId") roomId: Int,
        @Path("paramName") paramName: String,
        @Query("from") from: Long? = null,
        @Query("to") to: Long? = null,
        @Query("interval") interval: String = "hour"
    ): Response<DeviceHistoryResponse>

    @GET("ai/room/{roomId}")
    suspend fun getRoomAiInsights(@Path("roomId") roomId: Int): Response<RoomAiInsights>

    @GET("ai/room/{roomId}/control")
    suspend fun getRoomAiControl(@Path("roomId") roomId: Int): Response<AiControlSettings>

    @GET("notifications")
    suspend fun getNotifications(@Query("skip") skip: Int = 0, @Query("count") count: Int = 20): Response<PaginationResponse<UserNotification>>

    @GET("notifications/unread-count")
    suspend fun getUnreadNotificationCount(): Response<UnreadNotifications>

    @PATCH("notifications/{notificationId}/read")
    suspend fun markNotificationRead(@Path("notificationId") notificationId: Long): Response<Void>

    @PATCH("notifications/read-all")
    suspend fun markAllNotificationsRead(): Response<NotificationUpdateResponse>
}

data class NotificationUpdateResponse(val updated: Int)
