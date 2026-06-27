@file:OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)

package org.yooud.airsense.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ShowChart
import androidx.compose.material.icons.outlined.Air
import androidx.compose.material.icons.outlined.Dashboard
import androidx.compose.material.icons.outlined.DeviceThermostat
import androidx.compose.material.icons.outlined.Map
import androidx.compose.material.icons.outlined.Sensors
import androidx.compose.material.icons.outlined.Speed
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import org.yooud.airsense.env.RoomDetailViewModel
import org.yooud.airsense.models.Device
import org.yooud.airsense.models.DeviceHistoryResponse
import org.yooud.airsense.models.HistoryDevice
import org.yooud.airsense.models.HistoryPoint
import org.yooud.airsense.models.MetricSeverity
import org.yooud.airsense.models.Parameter
import org.yooud.airsense.models.Room
import org.yooud.airsense.models.RoomLayout
import org.yooud.airsense.models.RoomLayoutItem
import org.yooud.airsense.models.Sensor
import org.yooud.airsense.models.formatFanSpeed
import org.yooud.airsense.models.parameterLabel
import org.yooud.airsense.models.parameterUiState
import org.yooud.airsense.models.roomSeverity
import org.yooud.airsense.models.severityLabel

@Composable
fun RoomDetailScreen(
    environmentId: Int,
    roomId: Int,
    initialRoom: Room?,
    onBack: () -> Unit
) {
    val viewModel = remember(environmentId, roomId) { RoomDetailViewModel(environmentId, roomId) }
    val room by viewModel.room.collectAsState()
    val sensors by viewModel.sensors.collectAsState()
    val devices by viewModel.devices.collectAsState()
    val layout by viewModel.layout.collectAsState()
    val history by viewModel.history.collectAsState()
    val selectedParameter by viewModel.selectedParameter.collectAsState()
    val isRefreshing by viewModel.isRefreshing.collectAsState()
    val isLoadingEquipment by viewModel.isLoadingEquipment.collectAsState()
    val isLoadingLayout by viewModel.isLoadingLayout.collectAsState()
    val isLoadingHistory by viewModel.isLoadingHistory.collectAsState()
    val errorMessage by viewModel.errorMessage.collectAsState()
    val currentRoom = room ?: initialRoom
    var selectedTab by remember { mutableIntStateOf(0) }
    val tabs = listOf("Live", "Chart", "Layout")

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            text = currentRoom?.name ?: "Room",
                            style = MaterialTheme.typography.titleLarge,
                            color = MaterialTheme.colorScheme.onBackground,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                        Text(
                            text = "Room monitoring",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    navigationIconContentColor = MaterialTheme.colorScheme.primary
                )
            )
        },
        containerColor = MaterialTheme.colorScheme.background
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            TabRow(
                selectedTabIndex = selectedTab,
                modifier = Modifier.fillMaxWidth(),
                containerColor = MaterialTheme.colorScheme.background,
                contentColor = MaterialTheme.colorScheme.primary
            ) {
                tabs.forEachIndexed { index, title ->
                    Tab(
                        selected = selectedTab == index,
                        onClick = {
                            selectedTab = index
                            viewModel.onTabSelected(index)
                        },
                        text = { Text(title) },
                        icon = {
                            Icon(
                                imageVector = tabIcon(index),
                                contentDescription = null,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    )
                }
            }

            errorMessage?.let {
                Text(
                    text = it,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                )
            }

            when (selectedTab) {
                0 -> LiveRoomTab(
                    room = currentRoom,
                    layout = layout,
                    sensors = sensors,
                    devices = devices,
                    isLoading = isRefreshing || isLoadingEquipment
                )
                1 -> ChartRoomTab(
                    room = currentRoom,
                    selectedParameter = selectedParameter,
                    history = history,
                    isLoading = isLoadingHistory,
                    onSelectParameter = viewModel::selectParameter
                )
                2 -> LayoutRoomTab(layout = layout, sensors = sensors, devices = devices, isLoading = isLoadingLayout)
            }
        }
    }
}

@Composable
private fun LiveRoomTab(
    room: Room?,
    layout: RoomLayout?,
    sensors: List<Sensor>,
    devices: List<Device>,
    isLoading: Boolean
) {
    val mappedSensors = layout.mappedSensorItems()
    val mappedDevices = layout.mappedVentItems()

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            RoomSummaryCard(
                room = room,
                sensorsCount = sensors.size.takeIf { it > 0 } ?: mappedSensors.size,
                devicesCount = devices.size.takeIf { it > 0 } ?: mappedDevices.size
            )
        }
        item {
            SectionTitle("Live values")
        }
        if (room?.parameters.isNullOrEmpty()) {
            item {
                EmptyPanel("No live parameters available")
            }
        } else {
            room?.parameters.orEmpty().forEach { parameter ->
                item {
                    MetricCard(parameter = parameter)
                }
            }
        }
        item {
            SectionTitle("Ventilation")
        }
        if (devices.isEmpty()) {
            if (isLoading && mappedDevices.isEmpty()) {
                item { EmptyPanel("Loading ventilation devices...") }
            } else if (mappedDevices.isEmpty()) {
                item { EmptyPanel("No ventilation devices available") }
            } else {
                mappedDevices.forEach { item ->
                    item {
                        MappedEquipmentCard(
                            title = item.label ?: item.serialNumber ?: "Ventilation #${item.deviceId ?: "-"}",
                            subtitle = item.airflowRole?.replaceFirstChar { it.uppercaseChar() } ?: "Mapped ventilation device",
                            icon = Icons.Outlined.Air
                        )
                    }
                }
            }
        } else {
            devices.forEach { device ->
                item {
                    DeviceCard(device = device)
                }
            }
        }
        item {
            SectionTitle("Sensors")
        }
        if (sensors.isEmpty()) {
            if (isLoading && mappedSensors.isEmpty()) {
                item { EmptyPanel("Loading sensors...") }
            } else if (mappedSensors.isEmpty()) {
                item { EmptyPanel("No sensors available") }
            } else {
                mappedSensors.forEach { item ->
                    item {
                        MappedEquipmentCard(
                            title = item.label ?: item.serialNumber ?: "Sensor #${item.sensorId ?: "-"}",
                            subtitle = "Mapped sensor",
                            icon = Icons.Outlined.Sensors
                        )
                    }
                }
            }
        } else {
            sensors.forEach { sensor ->
                item {
                    SensorCard(sensor = sensor)
                }
            }
        }
    }
}

@Composable
private fun RoomSummaryCard(
    room: Room?,
    sensorsCount: Int,
    devicesCount: Int
) {
    val severity = room?.let(::roomSeverity) ?: MetricSeverity.UNKNOWN
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = severityLabel(severity),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = "Room #${room?.id ?: "-"}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                SeverityBadge(severity = severity)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SummaryChip(icon = Icons.Outlined.Sensors, text = "$sensorsCount sensors")
                SummaryChip(icon = Icons.Outlined.Air, text = "$devicesCount vents")
                SummaryChip(icon = Icons.Outlined.Speed, text = "Fan ${formatFanSpeed(room?.deviceSpeed)}")
            }
        }
    }
}

@Composable
private fun MappedEquipmentCard(
    title: String,
    subtitle: String,
    icon: ImageVector
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

@Composable
private fun SummaryChip(icon: ImageVector, text: String) {
    AssistChip(
        onClick = {},
        leadingIcon = {
            Icon(icon, contentDescription = null, modifier = Modifier.size(18.dp))
        },
        label = { Text(text) }
    )
}

@Composable
private fun MetricCard(parameter: Parameter) {
    val ui = parameterUiState(parameter)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.DeviceThermostat,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(22.dp)
                )
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = ui.label,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f)
                )
                Text(
                    text = ui.valueText,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    fontWeight = FontWeight.SemiBold
                )
            }
            LinearProgressIndicator(
                progress = { ui.progress },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(5.dp),
                color = severityColors(ui.severity).foreground,
                trackColor = MaterialTheme.colorScheme.surfaceVariant
            )
        }
    }
}

@Composable
private fun DeviceCard(device: Device) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(Icons.Outlined.Air, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "Ventilation #${device.id}",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = device.serialNumber,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            Text(
                text = formatFanSpeed(device.fanSpeed),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
        }
    }
}

@Composable
private fun SensorCard(sensor: Sensor) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Outlined.Sensors, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                Spacer(modifier = Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = sensor.typeName,
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = sensor.serialNumber,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            sensor.parameters.orEmpty().take(3).forEach { parameter ->
                val ui = parameterUiState(parameter)
                Row(verticalAlignment = Alignment.CenterVertically) {
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
        }
    }
}

@Composable
private fun ChartRoomTab(
    room: Room?,
    selectedParameter: String?,
    history: DeviceHistoryResponse?,
    isLoading: Boolean,
    onSelectParameter: (String) -> Unit
) {
    val parameters = buildList {
        addAll(room?.parameters.orEmpty().map { it.name })
        add("device_speed")
    }.distinct()

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            SectionTitle("24 hour trend")
        }
        item {
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                parameters.forEach { name ->
                    FilterChip(
                        selected = selectedParameter == name,
                        onClick = { onSelectParameter(name) },
                        label = { Text(parameterLabel(name)) }
                    )
                }
            }
        }
        item {
            HistoryChartCard(history = history, selectedParameter = selectedParameter, isLoading = isLoading)
        }
    }
}

@Composable
private fun HistoryChartCard(
    history: DeviceHistoryResponse?,
    selectedParameter: String?,
    isLoading: Boolean
) {
    val series = history?.data.orEmpty().filter { it.history.isNotEmpty() }
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(
                text = selectedParameter?.let(::parameterLabel) ?: "Trend",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface
            )
            if (isLoading && history == null) {
                EmptyPanel("Loading history...")
            } else if (series.isEmpty()) {
                EmptyPanel("No history data available")
            } else {
                Sparkline(series = series)
                series.take(4).forEachIndexed { index, source ->
                    val last = source.history.maxByOrNull { it.timestamp }?.value
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier
                                .size(9.dp)
                                .clip(CircleShape)
                                .background(chartSeriesColor(index))
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = source.serialNumber ?: "Source #${source.id}",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.weight(1f)
                        )
                        Text(
                            text = last?.let { "%.1f".format(it) } ?: "-",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun Sparkline(series: List<HistoryDevice>) {
    val gridColor = MaterialTheme.colorScheme.surfaceVariant
    val allPoints = series.flatMap { it.history }
    val minTimestamp = allPoints.minOfOrNull { it.timestamp } ?: 0L
    val maxTimestamp = allPoints.maxOfOrNull { it.timestamp } ?: minTimestamp + 1L
    val minValue = allPoints.minOfOrNull { it.value } ?: 0.0
    val maxValue = allPoints.maxOfOrNull { it.value } ?: minValue + 1.0
    val timestampRange = (maxTimestamp - minTimestamp).coerceAtLeast(1L).toFloat()
    val valueRange = (maxValue - minValue).coerceAtLeast(1.0).toFloat()

    Canvas(
        modifier = Modifier
            .fillMaxWidth()
            .height(180.dp)
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.background)
            .padding(8.dp)
    ) {
        repeat(4) { index ->
            val y = size.height * index / 3f
            drawLine(gridColor, Offset(0f, y), Offset(size.width, y), strokeWidth = 1f)
        }
        series.forEachIndexed { index, source ->
            val path = Path()
            source.history.sortedBy { it.timestamp }.forEachIndexed { pointIndex, point ->
                val x = ((point.timestamp - minTimestamp) / timestampRange) * size.width
                val y = size.height - (((point.value - minValue).toFloat() / valueRange) * size.height)
                if (pointIndex == 0) path.moveTo(x, y) else path.lineTo(x, y)
            }
            drawPath(
                path = path,
                color = chartSeriesColor(index),
                style = Stroke(width = 4f)
            )
        }
    }
}

private fun chartSeriesColor(index: Int): Color {
    val colors = listOf(
        Color(0xFF38BDF8),
        Color(0xFFF97316),
        Color(0xFF22C55E),
        Color(0xFFA855F7),
        Color(0xFFEF4444),
        Color(0xFFEAB308)
    )
    return colors[index % colors.size]
}

@Composable
private fun LayoutRoomTab(
    layout: RoomLayout?,
    sensors: List<Sensor>,
    devices: List<Device>,
    isLoading: Boolean
) {
    val mappedSensors = layout.mappedSensorItems()
    val mappedDevices = layout.mappedVentItems()
    val sensorsCount = sensors.size.takeIf { it > 0 } ?: mappedSensors.size
    val devicesCount = devices.size.takeIf { it > 0 } ?: mappedDevices.size

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            SectionTitle("Room layout")
        }
        item {
            if (layout == null) {
                EmptyPanel(if (isLoading) "Loading room layout..." else "Room layout is not available")
            } else {
                LayoutCanvas(layout = layout)
            }
        }
        item {
            SectionTitle("Mapped equipment")
        }
        item {
            Text(
                text = "$sensorsCount sensors, $devicesCount ventilation devices",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        if (mappedSensors.isNotEmpty() || mappedDevices.isNotEmpty()) {
            item {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    (mappedSensors + mappedDevices).forEach { item ->
                        Text(
                            text = item.label ?: item.serialNumber ?: item.type.replaceFirstChar { it.uppercaseChar() },
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun LayoutCanvas(layout: RoomLayout) {
    val boardBackground = MaterialTheme.colorScheme.background
    val borderColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.65f)
    val gridColor = MaterialTheme.colorScheme.surfaceVariant
    val aspect = (layout.width / layout.height.coerceAtLeast(1.0)).toFloat().coerceIn(0.7f, 2.2f)

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxWidth()
                .aspectRatio(aspect)
                .padding(12.dp)
        ) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawRect(color = boardBackground, size = size)
                drawRect(color = gridColor.copy(alpha = 0.22f), size = size)
                val points = layout.geometry.points
                if (points.isNotEmpty()) {
                    val path = Path()
                    points.forEachIndexed { index, point ->
                        val offset = Offset(
                            x = (point.x / layout.width).toFloat() * size.width,
                            y = (point.y / layout.height).toFloat() * size.height
                        )
                        if (index == 0) path.moveTo(offset.x, offset.y) else path.lineTo(offset.x, offset.y)
                    }
                    path.close()
                    drawPath(path, color = boardBackground)
                    drawPath(path, color = borderColor, style = Stroke(width = 3f))
                } else {
                    drawRect(color = boardBackground, size = size)
                    drawRect(color = borderColor, size = size, style = Stroke(width = 3f))
                }
            }

            layout.items.forEach { item ->
                val itemWidth = (maxWidth.value * (item.width / layout.width).toFloat()).dp.coerceAtLeast(38.dp)
                val itemHeight = (maxHeight.value * (item.height / layout.height).toFloat()).dp.coerceAtLeast(30.dp)
                val x = (maxWidth.value * (item.x / layout.width).toFloat())
                    .dp
                    .coerceIn(0.dp, (maxWidth - itemWidth).coerceAtLeast(0.dp))
                val y = (maxHeight.value * (item.y / layout.height).toFloat())
                    .dp
                    .coerceIn(0.dp, (maxHeight - itemHeight).coerceAtLeast(0.dp))
                LayoutItemBox(
                    item = item,
                    modifier = Modifier
                        .offset(x = x, y = y)
                        .widthIn(min = 38.dp)
                        .width(itemWidth)
                        .height(itemHeight)
                )
            }
        }
    }
}

@Composable
private fun LayoutItemBox(
    item: RoomLayoutItem,
    modifier: Modifier = Modifier
) {
    val accent = when (item.type) {
        "sensor" -> Color(0xFF38BDF8)
        "vent" -> Color(0xFF60A5FA)
        "window", "door" -> Color(0xFFF59E0B)
        "equipment" -> Color(0xFFF87171)
        "zone" -> Color(0xFF34D399)
        else -> MaterialTheme.colorScheme.primary
    }
    val label = item.shortLayoutLabel()

    Box(
        modifier = modifier
            .clip(RoundedCornerShape(6.dp))
            .background(accent.copy(alpha = 0.16f))
            .border(1.dp, accent.copy(alpha = 0.68f), RoundedCornerShape(6.dp))
            .padding(4.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
            textAlign = TextAlign.Center
        )
    }
}

private fun RoomLayout?.mappedSensorItems(): List<RoomLayoutItem> =
    this?.items.orEmpty().filter { it.type == "sensor" || it.sensorId != null }

private fun RoomLayout?.mappedVentItems(): List<RoomLayoutItem> =
    this?.items.orEmpty().filter { it.type == "vent" || it.deviceId != null }

private fun RoomLayoutItem.shortLayoutLabel(): String {
    val fullLabel = label ?: serialNumber.orEmpty()
    val code = Regex("""\b[SV]\d+\b""").find(fullLabel)?.value
    return when (type) {
        "sensor" -> code ?: sensorId?.let { "S#$it" } ?: "Sensor"
        "vent" -> code ?: deviceId?.let { "V#$it" } ?: "Vent"
        "window" -> "Window"
        "door" -> "Door"
        "equipment" -> fullLabel.layoutWordsFallback("Equipment")
        "zone" -> fullLabel.layoutWordsFallback("Zone")
        else -> fullLabel.layoutWordsFallback(type.replaceFirstChar { it.uppercaseChar() })
    }
}

private fun String.layoutWordsFallback(fallback: String): String {
    val words = trim()
        .split(Regex("""\s+"""))
        .filter { it.isNotBlank() && it != "#" }
    val concise = words.takeLast(if (words.size > 2) 2 else words.size).joinToString(" ")
    return concise.takeIf { it.isNotBlank() }?.take(14) ?: fallback
}

@Composable
private fun SectionTitle(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(top = 4.dp)
    )
}

@Composable
private fun EmptyPanel(text: String) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.7f))
            .padding(18.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

private fun tabIcon(index: Int): ImageVector = when (index) {
    0 -> Icons.Outlined.Dashboard
    1 -> Icons.AutoMirrored.Outlined.ShowChart
    else -> Icons.Outlined.Map
}

@Preview(showBackground = true)
@Composable
private fun MetricCardPreview() {
    ModernTheme {
        MetricCard(
            parameter = Parameter(
                name = "temperature",
                value = 24.2,
                unit = "°C",
                minValue = 10.0,
                maxValue = 40.0
            )
        )
    }
}
