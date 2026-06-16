package org.yooud.airsense.ui


import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

private val PurpleAccent = Color(0xFF7E22CE)
private val PurpleLight = Color(0xFF9D4EDD)
private val PurpleDark = Color(0xFF5B1891)

private val TealAccent = Color(0xFF14B8A6)
private val TealLight = Color(0xFF2DD4BF)
private val TealDark = Color(0xFF0E766F)

private val LightBackground = Color(0xFFF6F6F6)
private val LightSurface    = Color(0xFFFFFFFF)
private val DarkBackground  = Color(0xFF121212)
private val DarkSurface     = Color(0xFF1E1E1E)

private val TextOnLightSurface = Color(0xFF1F2937)
private val TextOnDarkSurface  = Color(0xFFF1F5F9)

private val DividerLight = Color(0xFFE5E7EB)
private val DividerDark  = Color(0xFF374151)

private val LightColors = lightColorScheme(
    primary       = PurpleAccent,
    onPrimary     = Color.White,
    primaryContainer   = PurpleLight.copy(alpha = 0.3f),
    onPrimaryContainer = PurpleAccent,

    secondary     = TealAccent,
    onSecondary   = Color.White,
    secondaryContainer   = TealLight.copy(alpha = 0.3f),
    onSecondaryContainer = TealAccent,

    background    = LightBackground,
    onBackground  = TextOnLightSurface,

    surface       = LightSurface,
    onSurface     = TextOnLightSurface,
    surfaceVariant = Color(0xFFF0F4F8),
    onSurfaceVariant = TextOnLightSurface,

    error         = Color(0xFFB00020),
    onError       = Color.White,
)

private val DarkColors = darkColorScheme(
    primary       = PurpleDark,
    onPrimary     = Color.Black,
    primaryContainer   = PurpleAccent.copy(alpha = 0.25f),
    onPrimaryContainer = Color.Black,

    secondary     = TealDark,
    onSecondary   = Color.Black,
    secondaryContainer   = TealAccent.copy(alpha = 0.25f),
    onSecondaryContainer = Color.Black,

    background    = DarkBackground,
    onBackground  = TextOnDarkSurface,

    surface       = DarkSurface,
    onSurface     = TextOnDarkSurface,
    surfaceVariant = Color(0xFF2E2E2E),
    onSurfaceVariant = TextOnDarkSurface,

    error         = Color(0xFFCF6679),
    onError       = Color.Black,
)

private val AppTypography = Typography(
    displayLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 57.sp,
        lineHeight = 64.sp,
        letterSpacing = (-0.25).sp
    ),
    displayMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 45.sp,
        lineHeight = 52.sp,
        letterSpacing = 0.sp
    ),
    displaySmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 36.sp,
        lineHeight = 44.sp,
        letterSpacing = 0.sp
    ),

    headlineLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 32.sp,
        lineHeight = 40.sp,
        letterSpacing = 0.sp
    ),
    headlineMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 28.sp,
        lineHeight = 36.sp,
        letterSpacing = 0.sp
    ),
    headlineSmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 24.sp,
        lineHeight = 32.sp,
        letterSpacing = 0.sp
    ),

    titleLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 22.sp,
        lineHeight = 28.sp,
        letterSpacing = 0.sp
    ),
    titleMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 16.sp,
        lineHeight = 24.sp,
        letterSpacing = 0.15.sp
    ),
    titleSmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp,
        letterSpacing = 0.1.sp
    ),

    bodyLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 24.sp,
        letterSpacing = 0.5.sp
    ),
    bodyMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 14.sp,
        lineHeight = 20.sp,
        letterSpacing = 0.25.sp
    ),
    bodySmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 12.sp,
        lineHeight = 16.sp,
        letterSpacing = 0.4.sp
    ),

    labelLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp,
        letterSpacing = 0.1.sp
    ),
    labelMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 12.sp,
        lineHeight = 16.sp,
        letterSpacing = 0.5.sp
    ),
    labelSmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 11.sp,
        lineHeight = 16.sp,
        letterSpacing = 0.5.sp
    )
)

@Composable
fun ModernTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colors = if (darkTheme) DarkColors else LightColors

    MaterialTheme(
        colorScheme = colors,
        typography  = AppTypography,
        shapes      = Shapes(
            small  = RoundedCornerShape(8.dp),
            medium = RoundedCornerShape(12.dp),
            large  = RoundedCornerShape(16.dp)
        ),
        content = content
    )
}