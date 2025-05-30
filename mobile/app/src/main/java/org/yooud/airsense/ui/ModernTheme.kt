package org.yooud.airsense.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val LightColors = lightColorScheme(
    primary       = Color(0xFF1E88E5), // Blue 600
    secondary     = Color(0xFF42A5F5), // Blue 400
    background    = Color(0xFFE3F2FD), // Light Blue Background
    surface       = Color(0xFFFFFFFF),
    onPrimary     = Color(0xFFFFFFFF),
    onSecondary   = Color(0xFF000000),
    onBackground  = Color(0xFF000000),
    onSurface     = Color(0xFF000000)
)

private val DarkColors = darkColorScheme(
    primary       = Color(0xFF90CAF9), // Blue 200
    secondary     = Color(0xFF64B5F6), // Blue 300
    background    = Color(0xFF0D47A1), // Dark Blue Background
    surface       = Color(0xFF121212),
    onPrimary     = Color(0xFF000000),
    onSecondary   = Color(0xFF000000),
    onBackground  = Color(0xFFFFFFFF),
    onSurface     = Color(0xFFFFFFFF)
)

@Composable
fun ModernTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        content     = content
    )
}
