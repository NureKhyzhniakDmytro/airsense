package org.yooud.airsense.fcm

import android.util.Log
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

interface MessagingRepository {
    suspend fun fetchFcmToken(): String
}

class FirebaseMessagingRepository : MessagingRepository {

    override suspend fun fetchFcmToken(): String = suspendCancellableCoroutine { cont ->
        FirebaseMessaging.getInstance().token
            .addOnCompleteListener { task ->
                if (!task.isSuccessful) {
                    val exception = task.exception
                        ?: RuntimeException("Failed to get FCM token (unknown error)")
                    Log.w("FCMToken", "Failed to get FCM token", exception)
                    if (cont.isActive) cont.resumeWithException(exception)
                    return@addOnCompleteListener
                }

                val token = task.result ?: ""
                Log.d("FCMToken", "FCM token: $token")
                if (cont.isActive) cont.resume(token)
            }
            .addOnFailureListener { exception ->
                Log.w("FCMToken", "Error while fetching FCM token", exception)
                if (cont.isActive) cont.resumeWithException(exception)
            }

        cont.invokeOnCancellation { }
    }
}
