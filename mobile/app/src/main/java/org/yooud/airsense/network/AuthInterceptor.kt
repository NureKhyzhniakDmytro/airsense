package org.yooud.airsense.network

import okhttp3.Interceptor
import okhttp3.Response
import org.yooud.airsense.auth.SessionManager

class AuthInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request().newBuilder().apply {
            SessionManager.token?.let { token ->
                addHeader("Authorization", "Bearer $token")
            }
        }.build()
        return chain.proceed(request)
    }
}
