package org.yooud.airsense.app

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import org.yooud.airsense.auth.FirebaseAuthRepository
import org.yooud.airsense.env.EnvironmentViewModel
import org.yooud.airsense.models.Environment
import org.yooud.airsense.ui.EnvironmentScreen
import org.yooud.airsense.ui.ModernTheme
import org.yooud.airsense.ui.SettingsScreen

class EnvironmentActivity : ComponentActivity() {
    private val viewModel: EnvironmentViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            EnvironmentActivityContent(
                viewModel = viewModel,
                onItemClick = { env ->

                },
                onLogout = {
                    FirebaseAuthRepository().signOut()
                    val intent = Intent(this, MainActivity::class.java)
                    startActivity(intent)
                }
            )
        }
    }
}

@Composable
private fun EnvironmentActivityContent(
    viewModel: EnvironmentViewModel,
    onItemClick: (Environment) -> Unit,
    onLogout: () -> Unit
) {
    var currentScreen by remember { mutableStateOf("list") }

    ModernTheme {
        when (currentScreen) {
            "list" -> EnvironmentScreen(
                onItemClick = onItemClick,
                viewModel = viewModel,
                onSettingsClick = { currentScreen = "settings"
                }
            )
            "settings" -> SettingsScreen(
                onBack = { currentScreen = "list" },
                onLogout = onLogout
            )
        }
    }
}
