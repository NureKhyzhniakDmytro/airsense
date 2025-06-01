package org.yooud.airsense.fcm

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.media.RingtoneManager
import android.os.Build
import android.util.Log
import androidx.core.app.NotificationCompat
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.runBlocking
import org.yooud.airsense.R
import org.yooud.airsense.app.EnvironmentActivity
import org.yooud.airsense.models.RegisterRequest
import org.yooud.airsense.network.ApiClient

class MyFirebaseMessagingService : FirebaseMessagingService() {

    companion object {
        private const val TAG = "MyFCMService"
        private const val CHANNEL_ID = "AirsenseChannel"
    }

    private var notificationId = 0

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        Log.d(TAG, "Firebase Messaging Service initialized")
    }

    override fun onNewToken(token: String) {
        super.onNewToken(token)
        Log.d(TAG, "New FCM token: $token")
        runBlocking {
            ApiClient.service.register(RegisterRequest(token))
        }
    }

    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        super.onMessageReceived(remoteMessage)
        Log.d(TAG, "Received message from: ${remoteMessage.from}")

        val notification = remoteMessage.notification
        if (notification != null) {
            val title = notification.title ?: "Airsense"
            val body = notification.body ?: ""
            sendNotification(title, body)
        } else if (remoteMessage.data.isNotEmpty()) {
            Log.d(TAG, "Data payload: ${remoteMessage.data}")
            val title = remoteMessage.data["title"] ?: "Airsense"
            val body = remoteMessage.data["body"] ?: ""
            sendNotification(title, body)
        }
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
            val channel = NotificationChannel(
                CHANNEL_ID,
                "Airsense Notifications",
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = "Канал уведомлений Airsense"
            }
            notificationManager.createNotificationChannel(channel)
        }
    }

    private fun sendNotification(title: String, messageBody: String) {
        Log.d(TAG, "Building notification with title: $title, body: $messageBody")

        val intent = Intent(this, EnvironmentActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
        }
        val pendingIntent = PendingIntent.getActivity(
            this, 0, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val defaultSoundUri = RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION)
        val notificationBuilder = NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.logo)
            .setContentTitle(title)
            .setContentText(messageBody)
            .setAutoCancel(true)
            .setSound(defaultSoundUri)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_HIGH)

        val notificationManager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        notificationManager.notify(notificationId++, notificationBuilder.build())
        Log.d(TAG, "Notification sent with ID: $notificationId")
    }
}
