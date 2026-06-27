@file:OptIn(ExperimentalMaterial3Api::class)

package org.yooud.airsense.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.outlined.Air
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Warning
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.flow.collectLatest
import org.yooud.airsense.env.EnvironmentDetailViewModel
import org.yooud.airsense.models.MetricSeverity
import org.yooud.airsense.models.Parameter
import org.yooud.airsense.models.Room
import org.yooud.airsense.models.formatFanSpeed
import org.yooud.airsense.models.parameterUiState
import org.yooud.airsense.models.roomSeverity
import org.yooud.airsense.models.severityLabel

@Composable
fun EnvironmentDetailScreen(
    environmentId: Int,
    environmentName: String?,
    onBack: () -> Unit,
    onRoomClick: (Room) -> Unit
) {
    val viewModel = remember(environmentId) { EnvironmentDetailViewModel(environmentId) }
    val rooms by viewModel.rooms.collectAsState()
    val isRefreshing by viewModel.isRefreshing.collectAsState()
    val isLoadingMore by viewModel.isLoadingMore.collectAsState()
    val hasMoreData by viewModel.hasMoreData.collectAsState()
    val errorMessage by viewModel.errorMessage.collectAsState()
    val listState = rememberLazyListState()

    LaunchedEffect(listState, hasMoreData) {
        snapshotFlow { listState.firstVisibleItemIndex to listState.layoutInfo.totalItemsCount }
            .collectLatest { (firstIndex, totalCount) ->
                if (hasMoreData && totalCount > 0 && firstIndex + 1 >= totalCount - 1) {
                    viewModel.loadMoreRooms()
                }
            }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            text = environmentName ?: "Environment",
                            style = MaterialTheme.typography.titleLarge,
                            color = MaterialTheme.colorScheme.onBackground
                        )
                        Text(
                            text = "${rooms.size} rooms loaded",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Back"
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    navigationIconContentColor = MaterialTheme.colorScheme.primary
                )
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
        modifier = Modifier.fillMaxSize()
    ) { innerPadding ->
        PullToRefreshWrapper(
            isRefreshing = isRefreshing,
            onRefresh = viewModel::refreshRooms,
            modifier = Modifier
                .padding(innerPadding)
                .fillMaxSize(),
            enabled = true
        ) {
            LazyColumn(
                state = listState,
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                errorMessage?.let { message ->
                    item {
                        Text(
                            text = message,
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(vertical = 8.dp)
                        )
                    }
                }

                if (rooms.isEmpty() && !isRefreshing && errorMessage == null) {
                    item {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 48.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "No rooms available",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                itemsIndexed(rooms, key = { _, room -> room.id }) { index, room ->
                    RoomCard(
                        room = room,
                        onClick = { onRoomClick(room) }
                    )
                    if (index < rooms.lastIndex) {
                        Spacer(modifier = Modifier.height(2.dp))
                    }
                }

                if (isLoadingMore) {
                    item {
                        LinearProgressIndicator(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 16.dp)
                                .height(4.dp),
                            color = MaterialTheme.colorScheme.primary
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun RoomCard(
    room: Room,
    onClick: () -> Unit
) {
    val severity = roomSeverity(room)
    val primaryParams = room.parameters.orEmpty().take(3)

    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = MaterialTheme.shapes.medium
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = room.name,
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(
                        text = "Room #${room.id}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                SeverityBadge(severity = severity)
                Spacer(modifier = Modifier.width(8.dp))
                Icon(
                    imageVector = Icons.Default.ChevronRight,
                    contentDescription = "Open room",
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(24.dp)
                )
            }

            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.fillMaxWidth()
            ) {
                AssistChip(
                    onClick = {},
                    leadingIcon = {
                        Icon(
                            imageVector = Icons.Outlined.Air,
                            contentDescription = null,
                            modifier = Modifier.size(18.dp)
                        )
                    },
                    label = { Text("Fan ${formatFanSpeed(room.deviceSpeed)}") }
                )
            }

            if (primaryParams.isEmpty()) {
                Text(
                    text = "No live parameters",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            } else {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    primaryParams.forEach { parameter ->
                        ParameterCompactRow(parameter = parameter)
                    }
                }
            }
        }
    }
}

@Composable
private fun ParameterCompactRow(parameter: Parameter) {
    val ui = parameterUiState(parameter)
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = ui.label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = ui.valueText,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
internal fun SeverityBadge(severity: MetricSeverity) {
    val colors = severityColors(severity)
    AssistChip(
        onClick = {},
        leadingIcon = {
            Icon(
                imageVector = statusIcon(severity),
                contentDescription = null,
                modifier = Modifier.size(18.dp),
                tint = colors.foreground
            )
        },
        label = { Text(severityLabel(severity)) }
    )
}

@Composable
internal fun severityColors(severity: MetricSeverity): StatusColors = when (severity) {
    MetricSeverity.NORMAL -> StatusColors(
        background = Color(0xFFE7F8F1),
        foreground = Color(0xFF047857)
    )
    MetricSeverity.WARNING -> StatusColors(
        background = Color(0xFFFFF7D6),
        foreground = Color(0xFFB45309)
    )
    MetricSeverity.CRITICAL -> StatusColors(
        background = MaterialTheme.colorScheme.errorContainer,
        foreground = MaterialTheme.colorScheme.onErrorContainer
    )
    MetricSeverity.UNKNOWN -> StatusColors(
        background = MaterialTheme.colorScheme.surfaceVariant,
        foreground = MaterialTheme.colorScheme.onSurfaceVariant
    )
}

internal data class StatusColors(
    val background: Color,
    val foreground: Color
)

private fun statusIcon(severity: MetricSeverity): ImageVector = when (severity) {
    MetricSeverity.NORMAL -> Icons.Outlined.CheckCircle
    MetricSeverity.WARNING -> Icons.Outlined.Warning
    MetricSeverity.CRITICAL -> Icons.Outlined.ErrorOutline
    MetricSeverity.UNKNOWN -> Icons.Outlined.Warning
}

@Preview(showBackground = true)
@Composable
private fun RoomCardPreview() {
    ModernTheme {
        RoomCard(
            room = Room(
                id = 12,
                name = "Assembly Line",
                icon = null,
                deviceSpeed = 42.0,
                parameters = listOf(
                    Parameter("temperature", 23.4, "°C", 10.0, 40.0),
                    Parameter("co2", 720.0, "ppm", 350.0, 2000.0),
                    Parameter("humidity", 44.0, "%", 0.0, 100.0)
                )
            ),
            onClick = {}
        )
    }
}
