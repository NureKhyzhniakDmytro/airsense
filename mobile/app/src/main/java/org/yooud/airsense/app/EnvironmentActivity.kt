package org.yooud.airsense.app

import android.Manifest
import android.content.Intent
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.util.Log
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import org.yooud.airsense.auth.FirebaseAuthRepository
import org.yooud.airsense.env.EnvironmentViewModel
import org.yooud.airsense.models.Environment
import org.yooud.airsense.models.Room
import org.yooud.airsense.ui.AppThemeMode
import org.yooud.airsense.ui.EnvironmentDetailScreen
import org.yooud.airsense.ui.EnvironmentScreen
import org.yooud.airsense.ui.ModernTheme
import org.yooud.airsense.ui.RoomDetailScreen
import org.yooud.airsense.ui.SettingsScreen

class EnvironmentActivity : ComponentActivity() {
    private val viewModel: EnvironmentViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            EnvironmentActivityContent(
                viewModel = viewModel,
                onLogout = {
                    FirebaseAuthRepository().signOut()
                    val intent = Intent(this, MainActivity::class.java).apply {
                        flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
                    }
                    startActivity(intent)
                    finish()
                }
            )
        }
    }
}

@Composable
private fun EnvironmentActivityContent(
    viewModel: EnvironmentViewModel,
    onLogout: () -> Unit
) {
    var currentScreen by remember { mutableStateOf("list") }
    var selectedEnvironmentId by remember { mutableIntStateOf(0) }
    var selectedRoomId by remember { mutableIntStateOf(0) }
    var selectedEnvironment by remember { mutableStateOf<Environment?>(null) }
    var selectedRoom by remember { mutableStateOf<Room?>(null) }
    val context = androidx.compose.ui.platform.LocalContext.current
    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission()
    ) { }
    var themeMode by remember {
        mutableStateOf(
            AppThemeMode.fromStorageValue(
                context.getSharedPreferences(THEME_PREFS, Context.MODE_PRIVATE)
                    .getString(THEME_MODE_KEY, null)
            )
        )
    }

    LaunchedEffect(Unit) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    ModernTheme(themeMode = themeMode) {
        when (currentScreen) {
            "list" -> EnvironmentScreen(
                onItemClick = { environment ->
                    currentScreen = "environment"
                    selectedEnvironmentId = environment.id
                    selectedEnvironment = environment
                },
                viewModel = viewModel,
                onSettingsClick = { currentScreen = "settings"
                }
            )
            "settings" -> SettingsScreen(
                onBack = { currentScreen = "list" },
                onLogout = onLogout,
                themeMode = themeMode,
                onThemeModeChange = { mode ->
                    themeMode = mode
                    context.getSharedPreferences(THEME_PREFS, Context.MODE_PRIVATE)
                        .edit()
                        .putString(THEME_MODE_KEY, mode.storageValue)
                        .apply()
                }
            )
            "environment" -> EnvironmentDetailScreen(
                environmentId = selectedEnvironmentId,
                environmentName = selectedEnvironment?.name,
                onBack = { currentScreen = "list" },
                onRoomClick = { room ->
                    selectedRoom = room
                    selectedRoomId = room.id
                    currentScreen = "room"
                }
            )
            "room" -> RoomDetailScreen(
                environmentId = selectedEnvironmentId,
                roomId = selectedRoomId,
                initialRoom = selectedRoom,
                onBack = { currentScreen = "environment" }
            )
        }
    }
}

private const val THEME_PREFS = "airsense_mobile_preferences"
private const val THEME_MODE_KEY = "theme_mode"
