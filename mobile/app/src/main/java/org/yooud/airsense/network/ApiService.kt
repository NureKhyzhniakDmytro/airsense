package org.yooud.airsense.network

import org.yooud.airsense.models.Environment
import org.yooud.airsense.models.PaginationResponse
import org.yooud.airsense.models.RegisterRequest
import org.yooud.airsense.models.Room
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface ApiService {
    @POST("auth")
    suspend fun register(@Body req: RegisterRequest): Response<Void>

    @GET("env")
    suspend fun getEnvironments(@Query("skip") skip: Int = 0, @Query("count") count: Int = 10): Response<PaginationResponse<Environment>>

    @GET("env/{id}/room")
    suspend fun getRooms(@Path("id") envId: Int, @Query("skip") skip: Int = 0, @Query("count") count: Int = 10): Response<PaginationResponse<Room>>
}
