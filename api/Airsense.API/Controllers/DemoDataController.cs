using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Models.Dto.Room;
using Airsense.API.Repository;
using Airsense.API.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("demo-data")]
[Authorize]
public class DemoDataController(
    IDbConnection connection,
    IMqttService mqttService,
    IRoomRepository roomRepository) : ControllerBase
{
    private const string DemoEnvironmentName = "AirSense Demo Environment";
    private const string DemoEnvironmentIcon = "industrial";
    private const string DemoOwnerEmail = "khijnyak.dima@gmail.com";
    private const string DemoRoomPrefix = "Demo Room";
    private const string DemoSensorLikePattern = "demo-room-%-microclimate%";
    private const string DemoDeviceLikePattern = "demo-room-%-ventilation%";
    private const int DefaultDemoSensorCount = 3;
    private const int DefaultDemoDeviceCount = 2;
    private static readonly JsonSerializerOptions RoomLayoutJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly string[] Scenarios =
    [
        "auto",
        "empty_room",
        "normal_usage",
        "crowded_room",
        "ventilation_failure",
        "night_mode",
        "critical_co2_event"
    ];

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        await EnsureDemoControlSchemaAsync();
        return Ok(await GetStatusInternalAsync());
    }

    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap([FromBody] DemoBootstrapRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        var roomCount = Math.Clamp(request?.RoomCount ?? 4, 1, 12);
        await BootstrapInternalAsync(roomCount);
        return Ok(await GetStatusInternalAsync());
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] DemoRoomCreateRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        var validationError = ValidateRoomCreateRequest(request);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (request?.Readings is not null)
        {
            var readingsError = ValidateReadings(request.Readings);
            if (readingsError is not null)
                return BadRequest(new { message = readingsError });
        }

        await EnsureDemoControlSchemaAsync();
        var roomName = NormalizeText(request?.Name, await GetNextRoomNameAsync());
        var roomIcon = NormalizeText(request?.Icon, "room");
        var sensorCount = request?.SensorCount ?? DefaultDemoSensorCount;
        var deviceCount = request?.DeviceCount ?? DefaultDemoDeviceCount;

        EnsureConnectionOpen();
        int roomId;
        using (var transaction = connection.BeginTransaction())
        {
            var context = await EnsureDemoTopologyContextAsync(transaction);
            roomId = await CreateDemoRoomAsync(context.EnvironmentId, roomName, roomIcon, transaction);
            await AddDemoAssetsAsync(roomId, context.SensorTypeId, sensorCount, deviceCount, transaction);
            await EnsureCo2CurveAsync(roomId, context.ParameterIds["co2"], transaction);
            await EnsureProfileAsync(roomId, transaction);
            await SyncDemoRoomLayoutAssetsAsync(roomId, transaction);
            transaction.Commit();
        }

        if (request?.Readings is not null)
            await ApplyRoomReadingsInternalAsync(roomId, request.Readings);

        return Ok(await GetStatusInternalAsync());
    }

    [HttpPatch("rooms/{roomId:int}")]
    public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] DemoRoomUpdateRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        if (string.IsNullOrWhiteSpace(request?.Name))
            return BadRequest(new { message = "Room name is required" });

        await EnsureDemoControlSchemaAsync();
        if (!await IsDemoRoomAsync(roomId))
            return NotFound(new { message = "Demo room not found" });

        await connection.ExecuteAsync(
            """
            UPDATE rooms
            SET name = @name,
                icon = @icon
            WHERE id = @roomId
            """,
            new
            {
                roomId,
                name = NormalizeText(request.Name, $"{DemoRoomPrefix} {roomId}"),
                icon = NormalizeText(request.Icon, "room")
            });

        return Ok(await GetStatusInternalAsync());
    }

    [HttpPost("rooms/{roomId:int}/assets")]
    public async Task<IActionResult> AddRoomAssets(int roomId, [FromBody] DemoRoomAssetsRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        var sensorCount = request?.SensorCount ?? 0;
        var deviceCount = request?.DeviceCount ?? 0;
        if (sensorCount < 0 || sensorCount > 20 || deviceCount < 0 || deviceCount > 20)
            return BadRequest(new { message = "Asset counts must be between 0 and 20" });
        if (sensorCount + deviceCount == 0)
            return BadRequest(new { message = "Add at least one sensor or ventilation device" });

        await EnsureDemoControlSchemaAsync();
        if (!await IsDemoRoomAsync(roomId))
            return NotFound(new { message = "Demo room not found" });

        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();
        var context = await EnsureDemoTopologyContextAsync(transaction);
        await AddDemoAssetsAsync(roomId, context.SensorTypeId, sensorCount, deviceCount, transaction);
        await EnsureCo2CurveAsync(roomId, context.ParameterIds["co2"], transaction);
        await EnsureProfileAsync(roomId, transaction);
        await SyncDemoRoomLayoutAssetsAsync(roomId, transaction);
        transaction.Commit();

        return Ok(await GetStatusInternalAsync());
    }

    [HttpPost("rooms/{roomId:int}/readings")]
    public async Task<IActionResult> ApplyRoomReadings(int roomId, [FromBody] DemoRoomReadingsRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        if (request is null)
            return BadRequest(new { message = "Provide at least one sensor or ventilation value" });

        var validationError = ValidateReadings(request);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        await EnsureDemoControlSchemaAsync();
        if (!await IsDemoRoomAsync(roomId))
            return NotFound(new { message = "Demo room not found" });

        var result = await ApplyRoomReadingsInternalAsync(roomId, request);
        if (result.SensorRows == 0 && HasSensorReadings(request))
            return BadRequest(new { message = "Room does not have demo sensors for these readings" });
        if (result.DeviceRows == 0 && request.VentilationPower.HasValue)
            return BadRequest(new { message = "Room does not have demo ventilation devices for this reading" });

        return Ok(await GetStatusInternalAsync());
    }

    [HttpPatch("rooms/{roomId:int}/emitters/{emitterId}")]
    public async Task<IActionResult> UpdateEmitter(int roomId, string emitterId, [FromBody] DemoEmitterUpdateRequest? request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        if (request?.HeatLoadKw is null || request.HeatLoadKw < 0 || request.HeatLoadKw > 1000)
            return BadRequest(new { message = "Heat load must be between 0 and 1000 kW" });

        await EnsureDemoControlSchemaAsync();
        if (!await IsDemoRoomAsync(roomId))
            return NotFound(new { message = "Demo room not found" });

        var layout = await roomRepository.GetLayoutAsync(roomId);
        var item = layout.Items.FirstOrDefault(item => (
            string.Equals(item.Id, emitterId, StringComparison.Ordinal) &&
            string.Equals(item.Type, "equipment", StringComparison.OrdinalIgnoreCase)
        ));

        if (item is null)
            return NotFound(new { message = "Emitter not found" });

        item.HeatLoadKw = Math.Round(request.HeatLoadKw.Value, 2);
        item.ThermalLoad = item.HeatLoadKw switch
        {
            >= 15 => "high",
            >= 5 => "medium",
            > 0 => "low",
            _ => item.ThermalLoad
        };

        await roomRepository.UpdateLayoutAsync(roomId, layout);
        return Ok(await GetStatusInternalAsync());
    }

    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill([FromBody] DemoBackfillRequest request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        if (!string.IsNullOrWhiteSpace(request.Scenario) && !Scenarios.Contains(request.Scenario))
            return BadRequest(new { message = "Unknown scenario" });

        await EnsureDemoControlSchemaAsync();
        var result = await BackfillInternalAsync(request);
        return Ok(result);
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ResetHistory()
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        await EnsureDemoControlSchemaAsync();
        var result = await connection.QuerySingleAsync<DemoResetResultDto>(
            """
            WITH deleted_sensor AS (
                DELETE FROM sensor_data
                WHERE sensor_id IN (
                    SELECT id FROM sensors WHERE serial_number LIKE @sensorPattern
                )
                RETURNING 1
            ),
            deleted_device AS (
                DELETE FROM device_data
                WHERE device_id IN (
                    SELECT id FROM devices WHERE serial_number LIKE @devicePattern
                )
                RETURNING 1
            )
            SELECT
                (SELECT COUNT(*) FROM deleted_sensor) AS DeletedSensorRows,
                (SELECT COUNT(*) FROM deleted_device) AS DeletedDeviceRows
            """,
            new
            {
                sensorPattern = DemoSensorLikePattern,
                devicePattern = DemoDeviceLikePattern
            });

        return Ok(result);
    }

    [HttpPatch("rooms/{roomId:int}/profile")]
    public async Task<IActionResult> UpdateRoomProfile(int roomId, [FromBody] DemoRoomProfileRequest request)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        if (!Scenarios.Contains(request.Scenario))
            return BadRequest(new { message = "Unknown scenario" });

        if (request.VentilationPowerOverride is < 0 or > 100)
            return BadRequest(new { message = "Ventilation override must be between 0 and 100" });

        if (request.OccupancyOverride is < 0 or > 100)
            return BadRequest(new { message = "Occupancy override must be between 0 and 100" });

        await EnsureDemoControlSchemaAsync();
        if (!await IsDemoRoomAsync(roomId))
            return NotFound(new { message = "Demo room not found" });

        await connection.ExecuteAsync(
            """
            INSERT INTO demo_room_profiles(room_id, scenario, ventilation_power_override, occupancy_override, updated_at)
            VALUES (@roomId, @scenario, @ventilationPowerOverride, @occupancyOverride, CURRENT_TIMESTAMP)
            ON CONFLICT (room_id) DO UPDATE SET
                scenario = EXCLUDED.scenario,
                ventilation_power_override = EXCLUDED.ventilation_power_override,
                occupancy_override = EXCLUDED.occupancy_override,
                updated_at = CURRENT_TIMESTAMP
            """,
            new
            {
                roomId,
                scenario = request.Scenario,
                ventilationPowerOverride = request.VentilationPowerOverride,
                occupancyOverride = request.OccupancyOverride
            });

        return Ok(await GetStatusInternalAsync());
    }

    [HttpDelete("rooms/{roomId:int}/profile")]
    public async Task<IActionResult> ClearRoomProfile(int roomId)
    {
        if (!HasRegisteredUser())
            return BadRequest(new { message = "You are not registered" });

        await EnsureDemoControlSchemaAsync();
        await connection.ExecuteAsync("DELETE FROM demo_room_profiles WHERE room_id = @roomId", new { roomId });
        return Ok(await GetStatusInternalAsync());
    }

    private bool HasRegisteredUser() => int.TryParse(User.FindFirstValue("id"), out _);

    private async Task EnsureDemoControlSchemaAsync()
    {
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS demo_room_profiles (
                room_id int PRIMARY KEY REFERENCES rooms(id) ON DELETE CASCADE,
                scenario varchar(64) NOT NULL DEFAULT 'auto',
                ventilation_power_override real,
                occupancy_override int,
                updated_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
    }

    private async Task<DemoDataStatusDto> GetStatusInternalAsync()
    {
        var environment = await connection.QueryFirstOrDefaultAsync<DemoEnvironmentDto>(
            """
            SELECT id AS Id, name AS Name, icon AS Icon
            FROM environments
            WHERE name = @name
            ORDER BY id
            LIMIT 1
            """,
            new { name = DemoEnvironmentName });

        var metrics = await connection.QuerySingleAsync<DemoMetricsDto>(
            """
            WITH demo_env AS (
                SELECT id FROM environments WHERE name = @name ORDER BY id LIMIT 1
            ),
            demo_rooms AS (
                SELECT r.id FROM rooms r JOIN demo_env e ON e.id = r.environment_id
            ),
            demo_sensors AS (
                SELECT s.id FROM sensors s JOIN demo_rooms r ON r.id = s.room_id
                WHERE s.serial_number LIKE @sensorPattern
            ),
            demo_devices AS (
                SELECT d.id FROM devices d JOIN demo_rooms r ON r.id = d.room_id
                WHERE d.serial_number LIKE @devicePattern
            )
            SELECT
                (SELECT COUNT(*) FROM demo_rooms) AS RoomCount,
                (SELECT COUNT(*) FROM demo_sensors) AS SensorCount,
                (SELECT COUNT(*) FROM demo_devices) AS DeviceCount,
                (SELECT COUNT(*) FROM sensor_data WHERE sensor_id IN (SELECT id FROM demo_sensors)) AS SensorDataRows,
                (SELECT COUNT(*) FROM device_data WHERE device_id IN (SELECT id FROM demo_devices)) AS DeviceDataRows,
                (SELECT MAX(timestamp) FROM sensor_data WHERE sensor_id IN (SELECT id FROM demo_sensors)) AS LatestTelemetryAt,
                (SELECT MAX(timestamp) FROM device_data WHERE device_id IN (SELECT id FROM demo_devices)) AS LatestDeviceAt
            """,
            new
            {
                name = DemoEnvironmentName,
                sensorPattern = DemoSensorLikePattern,
                devicePattern = DemoDeviceLikePattern
            });

        var rooms = await connection.QueryAsync<DemoRoomStatusDto>(
            """
            WITH demo_env AS (
                SELECT id FROM environments WHERE name = @name ORDER BY id LIMIT 1
            ),
            latest_sensor_per_sensor AS (
                SELECT DISTINCT ON (s.id, p.name)
                    s.room_id,
                    s.id AS SensorId,
                    p.name,
                    sd.value,
                    sd.timestamp
                FROM sensors s
                JOIN sensor_data sd ON sd.sensor_id = s.id
                JOIN parameters p ON p.id = sd.parameter_id
                WHERE s.serial_number LIKE @sensorPattern
                ORDER BY s.id, p.name, sd.timestamp DESC, sd.id DESC
            ),
            sensor_pivot AS (
                SELECT
                    room_id,
                    AVG(value) FILTER (WHERE name = 'co2') AS Co2,
                    AVG(value) FILTER (WHERE name = 'temperature') AS Temperature,
                    AVG(value) FILTER (WHERE name = 'humidity') AS Humidity,
                    MAX(timestamp) AS LastTelemetryAt
                FROM latest_sensor_per_sensor
                GROUP BY room_id
            ),
            latest_device_per_device AS (
                SELECT DISTINCT ON (d.id)
                    d.room_id,
                    d.id,
                    dd.value AS VentilationPower,
                    dd.timestamp AS LastDeviceAt
                FROM devices d
                JOIN device_data dd ON dd.device_id = d.id
                WHERE d.serial_number LIKE @devicePattern
                ORDER BY d.id, dd.timestamp DESC, dd.id DESC
            ),
            latest_device AS (
                SELECT
                    room_id,
                    AVG(VentilationPower) AS VentilationPower,
                    MAX(LastDeviceAt) AS LastDeviceAt
                FROM latest_device_per_device
                GROUP BY room_id
            )
            SELECT
                r.id AS RoomId,
                r.name AS RoomName,
                r.icon AS RoomIcon,
                sensor_assets.SensorCount AS SensorCount,
                sensor_assets.SensorSerialNumber AS SensorSerialNumber,
                sensor_assets.SensorSerialNumbers AS SensorSerialNumbers,
                device_assets.DeviceCount AS DeviceCount,
                device_assets.DeviceSerialNumber AS DeviceSerialNumber,
                device_assets.DeviceSerialNumbers AS DeviceSerialNumbers,
                COALESCE(p.scenario, 'auto') AS Scenario,
                p.ventilation_power_override AS VentilationPowerOverride,
                p.occupancy_override AS OccupancyOverride,
                sp.Co2 AS Co2,
                sp.Temperature AS Temperature,
                sp.Humidity AS Humidity,
                ld.VentilationPower AS VentilationPower,
                CASE
                    WHEN sp.LastTelemetryAt IS NULL THEN ld.LastDeviceAt
                    WHEN ld.LastDeviceAt IS NULL THEN sp.LastTelemetryAt
                    WHEN sp.LastTelemetryAt > ld.LastDeviceAt THEN sp.LastTelemetryAt
                    ELSE ld.LastDeviceAt
                END AS LastActivityAt
            FROM rooms r
            JOIN demo_env e ON e.id = r.environment_id
            LEFT JOIN LATERAL (
                SELECT
                    COUNT(*)::int AS SensorCount,
                    MIN(serial_number) AS SensorSerialNumber,
                    STRING_AGG(serial_number, ', ' ORDER BY id) AS SensorSerialNumbers
                FROM sensors
                WHERE room_id = r.id AND serial_number LIKE @sensorPattern
            ) sensor_assets ON TRUE
            LEFT JOIN LATERAL (
                SELECT
                    COUNT(*)::int AS DeviceCount,
                    MIN(serial_number) AS DeviceSerialNumber,
                    STRING_AGG(serial_number, ', ' ORDER BY id) AS DeviceSerialNumbers
                FROM devices
                WHERE room_id = r.id AND serial_number LIKE @devicePattern
            ) device_assets ON TRUE
            LEFT JOIN demo_room_profiles p ON p.room_id = r.id
            LEFT JOIN sensor_pivot sp ON sp.room_id = r.id
            LEFT JOIN latest_device ld ON ld.room_id = r.id
            ORDER BY r.id
            """,
            new
            {
                name = DemoEnvironmentName,
                sensorPattern = DemoSensorLikePattern,
                devicePattern = DemoDeviceLikePattern
            });

        var roomList = rooms.ToList();
        var emittersByRoom = await GetDemoEmittersAsync(roomList.Select(room => room.RoomId).ToArray());
        foreach (var room in roomList)
            room.Emitters = emittersByRoom.GetValueOrDefault(room.RoomId, []);

        return new DemoDataStatusDto
        {
            Environment = environment,
            Metrics = metrics,
            Rooms = roomList,
            Scenarios = Scenarios
        };
    }

    private async Task<Dictionary<int, List<DemoEmitterDto>>> GetDemoEmittersAsync(IReadOnlyCollection<int> roomIds)
    {
        if (roomIds.Count == 0)
            return [];

        var layouts = await connection.QueryAsync<(int RoomId, string LayoutJson)>(
            "SELECT id AS RoomId, layout::text AS LayoutJson FROM rooms WHERE id = ANY(@roomIds)",
            new { roomIds = roomIds.ToArray() });

        var result = new Dictionary<int, List<DemoEmitterDto>>();
        foreach (var layout in layouts)
        {
            var emitters = ExtractDemoEmitters(layout.LayoutJson);
            if (emitters.Count > 0)
                result[layout.RoomId] = emitters;
        }

        return result;
    }

    private static List<DemoEmitterDto> ExtractDemoEmitters(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
            return [];

        try
        {
            var layout = JsonSerializer.Deserialize<RoomLayoutDto>(layoutJson, RoomLayoutJsonOptions);
            return layout?.Items
                .Where(item => string.Equals(item.Type, "equipment", StringComparison.OrdinalIgnoreCase))
                .Select(item => new DemoEmitterDto
                {
                    Id = item.Id,
                    Label = string.IsNullOrWhiteSpace(item.Label) ? item.Id : item.Label,
                    HeatLoadKw = item.HeatLoadKw,
                    ThermalLoad = item.ThermalLoad
                })
                .OrderBy(item => item.Label)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task BootstrapInternalAsync(int roomCount)
    {
        await EnsureDemoControlSchemaAsync();
        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();

        var context = await EnsureDemoTopologyContextAsync(transaction);

        for (var index = 1; index <= roomCount; index++)
        {
            var roomId = await EnsureRoomAsync(
                context.EnvironmentId,
                $"{DemoRoomPrefix} {index}",
                index % 2 == 0 ? "office" : "production",
                transaction,
                index);
            await EnsureDemoAssetCountsAsync(
                roomId,
                context.SensorTypeId,
                DefaultDemoSensorCount,
                DefaultDemoDeviceCount,
                transaction,
                index);
            await EnsureCo2CurveAsync(roomId, context.ParameterIds["co2"], transaction);
            await EnsureProfileAsync(roomId, transaction);
            await SyncDemoRoomLayoutAssetsAsync(roomId, transaction);
        }

        transaction.Commit();
    }

    private async Task<DemoBackfillResultDto> BackfillInternalAsync(DemoBackfillRequest request)
    {
        var hours = Math.Clamp(request.Hours ?? 6, 1, 168);
        var intervalMinutes = Math.Clamp(request.IntervalMinutes ?? 5, 1, 60);
        var scenarioOverride = string.IsNullOrWhiteSpace(request.Scenario) ? null : request.Scenario;

        var roomCount = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM rooms r
            JOIN environments e ON e.id = r.environment_id
            WHERE e.name = @name
            """,
            new { name = DemoEnvironmentName });

        if (roomCount == 0)
            await BootstrapInternalAsync(4);

        var rooms = (await connection.QueryAsync<DemoBackfillRoomDto>(
            """
            SELECT
                r.id AS RoomId,
                COALESCE(p.scenario, 'auto') AS Scenario,
                p.ventilation_power_override AS VentilationPowerOverride,
                p.occupancy_override AS OccupancyOverride
            FROM rooms r
            JOIN environments e ON e.id = r.environment_id
            LEFT JOIN demo_room_profiles p ON p.room_id = r.id
            WHERE e.name = @name
            ORDER BY r.id
            """,
            new { name = DemoEnvironmentName })).ToList();

        var sensorIdsByRoom = (await connection.QueryAsync<DemoRoomAssetIdDto>(
            """
            SELECT r.id AS RoomId, s.id AS AssetId
            FROM rooms r
            JOIN environments e ON e.id = r.environment_id
            JOIN sensors s ON s.room_id = r.id AND s.serial_number LIKE @sensorPattern
            WHERE e.name = @name
            ORDER BY r.id, s.id
            """,
            new { name = DemoEnvironmentName, sensorPattern = DemoSensorLikePattern }))
            .GroupBy(asset => asset.RoomId)
            .ToDictionary(group => group.Key, group => group.Select(asset => asset.AssetId).ToList());

        var deviceIdsByRoom = (await connection.QueryAsync<DemoRoomAssetIdDto>(
            """
            SELECT r.id AS RoomId, d.id AS AssetId
            FROM rooms r
            JOIN environments e ON e.id = r.environment_id
            JOIN devices d ON d.room_id = r.id AND d.serial_number LIKE @devicePattern
            WHERE e.name = @name
            ORDER BY r.id, d.id
            """,
            new { name = DemoEnvironmentName, devicePattern = DemoDeviceLikePattern }))
            .GroupBy(asset => asset.RoomId)
            .ToDictionary(group => group.Key, group => group.Select(asset => asset.AssetId).ToList());

        var parameterIds = (await connection.QueryAsync<(int Id, string Name)>(
            "SELECT id AS Id, name AS Name FROM parameters WHERE name = ANY(@names)",
            new { names = new[] { "co2", "temperature", "humidity" } }))
            .ToDictionary(x => x.Name, x => x.Id);

        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();

        var now = DateTime.UtcNow;
        var from = now.AddHours(-hours);
        var random = new Random(37);
        var sensorRows = 0;
        var deviceRows = 0;

        foreach (var room in rooms)
        {
            var state = CreateInitialBackfillState(room.RoomId);
            for (var timestamp = from; timestamp <= now; timestamp = timestamp.AddMinutes(intervalMinutes))
            {
                var scenario = scenarioOverride ?? (room.Scenario == "auto" ? ChooseScenario(room.RoomId, timestamp) : room.Scenario);
                ApplyScenario(state, scenario, room, intervalMinutes, random);

                if (sensorIdsByRoom.TryGetValue(room.RoomId, out var sensorIds))
                {
                    foreach (var sensorId in sensorIds)
                    {
                        await InsertSensorValueAsync(sensorId, parameterIds["co2"], state.Co2, timestamp, transaction);
                        await InsertSensorValueAsync(sensorId, parameterIds["temperature"], state.Temperature, timestamp, transaction);
                        await InsertSensorValueAsync(sensorId, parameterIds["humidity"], state.Humidity, timestamp, transaction);
                        sensorRows += 3;
                    }
                }

                if (deviceIdsByRoom.TryGetValue(room.RoomId, out var deviceIds))
                {
                    foreach (var deviceId in deviceIds)
                    {
                        await connection.ExecuteAsync(
                            """
                            INSERT INTO device_data(device_id, timestamp, value, applied, applied_at)
                            VALUES (@deviceId, @timestamp, @value, TRUE, @timestamp)
                            """,
                            new { deviceId, timestamp, value = Math.Round(state.VentilationPower, 2) },
                            transaction);
                        deviceRows++;
                    }
                }
            }
        }

        transaction.Commit();
        return new DemoBackfillResultDto
        {
            InsertedSensorRows = sensorRows,
            InsertedDeviceRows = deviceRows,
            From = from,
            To = now
        };
    }

    private async Task<DemoTopologyContext> EnsureDemoTopologyContextAsync(IDbTransaction transaction)
    {
        var parameterIds = new Dictionary<string, int>
        {
            ["temperature"] = await EnsureParameterAsync("temperature", "°C", -50, 50, transaction),
            ["humidity"] = await EnsureParameterAsync("humidity", "%", 0, 100, transaction),
            ["co2"] = await EnsureParameterAsync("co2", "ppm", 300, 5000, transaction)
        };
        var typeId = await EnsureSensorTypeAsync("Microclimate Sensor", parameterIds.Values, transaction);
        var envId = await EnsureEnvironmentAsync(transaction);

        return new DemoTopologyContext
        {
            EnvironmentId = envId,
            SensorTypeId = typeId,
            ParameterIds = parameterIds
        };
    }

    private async Task<string> GetNextRoomNameAsync()
    {
        var index = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*) + 1
            FROM rooms r
            JOIN environments e ON e.id = r.environment_id
            WHERE e.name = @name
            """,
            new { name = DemoEnvironmentName });

        return $"{DemoRoomPrefix} {index}";
    }

    private async Task<int> CreateDemoRoomAsync(int envId, string name, string icon, IDbTransaction transaction)
    {
        return await connection.QuerySingleAsync<int>(
            """
            INSERT INTO rooms(name, environment_id, icon)
            VALUES (@name, @envId, @icon)
            RETURNING id
            """,
            new { envId, name, icon },
            transaction);
    }

    private async Task AddDemoAssetsAsync(
        int roomId,
        int sensorTypeId,
        int sensorCount,
        int deviceCount,
        IDbTransaction transaction,
        int? demoSlot = null)
    {
        var existingSensorCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sensors WHERE room_id = @roomId AND serial_number LIKE @sensorPattern",
            new { roomId, sensorPattern = DemoSensorLikePattern },
            transaction);
        var existingDeviceCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM devices WHERE room_id = @roomId AND serial_number LIKE @devicePattern",
            new { roomId, devicePattern = DemoDeviceLikePattern },
            transaction);
        var serialGroup = demoSlot ?? roomId;

        for (var index = 1; index <= sensorCount; index++)
        {
            var serialIndex = existingSensorCount + index;
            await EnsureSensorAsync(
                CreateDemoAssetSerial(serialGroup, "microclimate", serialIndex),
                sensorTypeId,
                roomId,
                transaction);
        }

        for (var index = 1; index <= deviceCount; index++)
        {
            var serialIndex = existingDeviceCount + index;
            await EnsureDeviceAsync(
                CreateDemoAssetSerial(serialGroup, "ventilation", serialIndex),
                roomId,
                transaction);
        }
    }

    private async Task EnsureDemoAssetCountsAsync(
        int roomId,
        int sensorTypeId,
        int targetSensorCount,
        int targetDeviceCount,
        IDbTransaction transaction,
        int? demoSlot = null)
    {
        var existingSensorCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sensors WHERE room_id = @roomId AND serial_number LIKE @sensorPattern",
            new { roomId, sensorPattern = DemoSensorLikePattern },
            transaction);
        var existingDeviceCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM devices WHERE room_id = @roomId AND serial_number LIKE @devicePattern",
            new { roomId, devicePattern = DemoDeviceLikePattern },
            transaction);

        await AddDemoAssetsAsync(
            roomId,
            sensorTypeId,
            Math.Max(0, targetSensorCount - existingSensorCount),
            Math.Max(0, targetDeviceCount - existingDeviceCount),
            transaction,
            demoSlot);
    }

    private async Task<DemoReadingsApplyResultDto> ApplyRoomReadingsInternalAsync(int roomId, DemoRoomReadingsRequest request)
    {
        var parameterIds = (await connection.QueryAsync<(int Id, string Name)>(
            "SELECT id AS Id, name AS Name FROM parameters WHERE name = ANY(@names)",
            new { names = new[] { "co2", "temperature", "humidity" } }))
            .ToDictionary(x => x.Name, x => x.Id);

        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();
        var context = await EnsureDemoTopologyContextAsync(transaction);
        foreach (var parameterId in context.ParameterIds)
            parameterIds.TryAdd(parameterId.Key, parameterId.Value);

        var sensors = (await connection.QueryAsync<DemoRoomAssetLiveDto>(
            """
            SELECT id AS Id, serial_number AS SerialNumber
            FROM sensors
            WHERE room_id = @roomId AND serial_number LIKE @sensorPattern
            ORDER BY id
            """,
            new { roomId, sensorPattern = DemoSensorLikePattern },
            transaction)).ToList();
        var devices = (await connection.QueryAsync<DemoRoomAssetLiveDto>(
            """
            SELECT id AS Id, serial_number AS SerialNumber
            FROM devices
            WHERE room_id = @roomId AND serial_number LIKE @devicePattern
            ORDER BY id
            """,
            new { roomId, devicePattern = DemoDeviceLikePattern },
            transaction)).ToList();

        var timestamp = DateTime.UtcNow;
        var sensorRows = 0;
        var deviceRows = 0;

        foreach (var sensor in sensors)
        {
            if (request.Co2.HasValue)
            {
                await InsertSensorValueAsync(sensor.Id, parameterIds["co2"], request.Co2.Value, timestamp, transaction);
                sensorRows++;
            }
            if (request.Temperature.HasValue)
            {
                await InsertSensorValueAsync(sensor.Id, parameterIds["temperature"], request.Temperature.Value, timestamp, transaction);
                sensorRows++;
            }
            if (request.Humidity.HasValue)
            {
                await InsertSensorValueAsync(sensor.Id, parameterIds["humidity"], request.Humidity.Value, timestamp, transaction);
                sensorRows++;
            }
        }

        if (request.VentilationPower.HasValue)
        {
            foreach (var device in devices)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO device_data(device_id, timestamp, value, applied, applied_at)
                    VALUES (@deviceId, @timestamp, @value, TRUE, @timestamp)
                    """,
                    new { deviceId = device.Id, timestamp, value = Math.Round(request.VentilationPower.Value, 2) },
                    transaction);
                deviceRows++;
            }
        }

        transaction.Commit();
        await PublishRoomReadingsLiveAsync(roomId, request, sensors, devices, timestamp);
        return new DemoReadingsApplyResultDto
        {
            SensorRows = sensorRows,
            DeviceRows = deviceRows
        };
    }

    private async Task PublishRoomReadingsLiveAsync(
        int roomId,
        DemoRoomReadingsRequest request,
        IReadOnlyCollection<DemoRoomAssetLiveDto> sensors,
        IReadOnlyCollection<DemoRoomAssetLiveDto> devices,
        DateTime timestamp)
    {
        var sentAt = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
        var sensorReadings = new List<(string Parameter, double Value)>();
        if (request.Co2.HasValue)
            sensorReadings.Add(("co2", request.Co2.Value));
        if (request.Temperature.HasValue)
            sensorReadings.Add(("temperature", request.Temperature.Value));
        if (request.Humidity.HasValue)
            sensorReadings.Add(("humidity", request.Humidity.Value));

        foreach (var sensor in sensors)
        {
            foreach (var reading in sensorReadings)
            {
                await mqttService.PublishAsync("airsense/sensor-state", new TelemetryEventDto
                {
                    RoomId = roomId,
                    SensorId = sensor.Id,
                    SerialNumber = sensor.SerialNumber,
                    Parameter = reading.Parameter,
                    Data = new()
                    {
                        Value = reading.Value,
                        SentAt = sentAt
                    }
                });
            }
        }

        if (!request.VentilationPower.HasValue)
            return;

        var value = Math.Round(request.VentilationPower.Value, 2);
        foreach (var device in devices)
        {
            await mqttService.PublishAsync("airsense/device-state", new DeviceTelemetryEventDto
            {
                RoomId = roomId,
                DeviceId = device.Id,
                SerialNumber = device.SerialNumber,
                FanSpeed = value,
                ActiveAt = sentAt,
                Source = "demo-control"
            });
        }
    }

    private static string CreateDemoAssetSerial(int demoSlot, string suffix, int index)
    {
        var serial = $"demo-room-{demoSlot}-{suffix}";
        return index <= 1 ? serial : $"{serial}-{index}";
    }

    private static string[] CreateDemoSlotSerials(int demoSlot)
    {
        var serials = new List<string>();
        for (var index = 1; index <= DefaultDemoSensorCount; index++)
            serials.Add(CreateDemoAssetSerial(demoSlot, "microclimate", index));
        for (var index = 1; index <= DefaultDemoDeviceCount; index++)
            serials.Add(CreateDemoAssetSerial(demoSlot, "ventilation", index));
        return serials.ToArray();
    }

    private static string NormalizeText(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string? ValidateRoomCreateRequest(DemoRoomCreateRequest? request)
    {
        if (request?.SensorCount is < 0 or > 20)
            return "Sensor count must be between 0 and 20";
        if (request?.DeviceCount is < 0 or > 20)
            return "Ventilation device count must be between 0 and 20";
        if ((request?.SensorCount ?? DefaultDemoSensorCount) + (request?.DeviceCount ?? DefaultDemoDeviceCount) == 0)
            return "Create at least one sensor or ventilation device";

        return null;
    }

    private static string? ValidateReadings(DemoRoomReadingsRequest request)
    {
        if (!HasSensorReadings(request) && !request.VentilationPower.HasValue)
            return "Provide at least one sensor or ventilation value";
        if (request.Co2 is < 300 or > 5000)
            return "CO2 must be between 300 and 5000 ppm";
        if (request.Temperature is < -50 or > 50)
            return "Temperature must be between -50 and 50 °C";
        if (request.Humidity is < 0 or > 100)
            return "Humidity must be between 0 and 100%";
        if (request.VentilationPower is < 0 or > 100)
            return "Ventilation power must be between 0 and 100%";

        return null;
    }

    private static bool HasSensorReadings(DemoRoomReadingsRequest request)
    {
        return request.Co2.HasValue
               || request.Temperature.HasValue
               || request.Humidity.HasValue;
    }

    private async Task<int> EnsureParameterAsync(string name, string unit, double minValue, double maxValue, IDbTransaction transaction)
    {
        var parameterId = await connection.QuerySingleAsync<int>(
            """
            WITH inserted AS (
                INSERT INTO parameters(name, unit, min_value, max_value)
                SELECT @name, @unit, @minValue, @maxValue
                WHERE NOT EXISTS (SELECT 1 FROM parameters WHERE name = @name)
                RETURNING id
            )
            SELECT id FROM inserted
            UNION ALL
            SELECT id FROM parameters WHERE name = @name
            LIMIT 1
            """,
            new { name, unit, minValue, maxValue },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE parameters
            SET unit = @unit,
                min_value = @minValue,
                max_value = @maxValue
            WHERE id = @parameterId
              AND (
                unit IS DISTINCT FROM @unit
                OR min_value IS DISTINCT FROM @minValue
                OR max_value IS DISTINCT FROM @maxValue
              )
            """,
            new { parameterId, unit, minValue, maxValue },
            transaction);

        return parameterId;
    }

    private async Task<int> EnsureSensorTypeAsync(string name, IEnumerable<int> parameterIds, IDbTransaction transaction)
    {
        var requestedParameterIds = parameterIds.Distinct().ToArray();
        var typeId = await connection.QuerySingleAsync<int>(
            """
            WITH inserted AS (
                INSERT INTO sensor_types(name)
                SELECT @name
                WHERE NOT EXISTS (SELECT 1 FROM sensor_types WHERE name = @name)
                RETURNING id
            )
            SELECT id FROM inserted
            UNION ALL
            SELECT id FROM sensor_types WHERE name = @name
            LIMIT 1
            """,
            new { name },
            transaction);

        await connection.ExecuteAsync(
            """
            DELETE FROM sensor_type_parameters
            WHERE type_id = @typeId
              AND NOT (parameter_id = ANY(@requestedParameterIds))
            """,
            new { typeId, requestedParameterIds },
            transaction);

        foreach (var parameterId in requestedParameterIds)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO sensor_type_parameters(type_id, parameter_id)
                SELECT @typeId, @parameterId
                WHERE NOT EXISTS (
                    SELECT 1 FROM sensor_type_parameters
                    WHERE type_id = @typeId AND parameter_id = @parameterId
                )
                """,
                new { typeId, parameterId },
                transaction);
        }

        return typeId;
    }

    private async Task<int> EnsureEnvironmentAsync(IDbTransaction transaction)
    {
        var envId = await connection.QuerySingleAsync<int>(
            """
            WITH inserted AS (
                INSERT INTO environments(name, icon)
                SELECT @name, @icon
                WHERE NOT EXISTS (SELECT 1 FROM environments WHERE name = @name)
                RETURNING id
            )
            SELECT id FROM inserted
            UNION ALL
            SELECT id FROM environments WHERE name = @name ORDER BY id LIMIT 1
            """,
            new { name = DemoEnvironmentName, icon = DemoEnvironmentIcon },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE environments
            SET icon = @icon
            WHERE id = @envId
              AND (icon IS NULL OR icon IN ('factory', 'building', ''))
            """,
            new { envId, icon = DemoEnvironmentIcon },
            transaction);

        await connection.ExecuteAsync(
            """
            WITH demo_owner AS (
                INSERT INTO users(uid, name, email)
                VALUES (@uid, @name, @email)
                ON CONFLICT (email) DO UPDATE SET
                    name = EXCLUDED.name
                RETURNING id
            )
            INSERT INTO environment_members(member_id, environment_id, role)
            SELECT demo_owner.id, @envId, 'owner'
            FROM demo_owner
            ON CONFLICT (member_id, environment_id) DO UPDATE SET
                role = EXCLUDED.role
            """,
            new
            {
                envId,
                uid = $"pending:{DemoOwnerEmail}",
                name = DemoOwnerEmail,
                email = DemoOwnerEmail
            },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO environment_members(member_id, environment_id, role)
            SELECT id, @envId, 'user'
            FROM users
            ON CONFLICT (member_id, environment_id) DO NOTHING
            """,
            new { envId },
            transaction);

        return envId;
    }

    private async Task<int> EnsureRoomAsync(
        int envId,
        string name,
        string icon,
        IDbTransaction transaction,
        int? demoSlot = null)
    {
        if (demoSlot.HasValue)
        {
            var roomIdByAssets = await connection.QuerySingleOrDefaultAsync<int?>(
                """
                SELECT asset_rooms.room_id
                FROM (
                    SELECT s.room_id
                    FROM sensors s
                    JOIN rooms r ON r.id = s.room_id
                    WHERE r.environment_id = @envId
                      AND s.serial_number = ANY(@serials)
                    UNION ALL
                    SELECT d.room_id
                    FROM devices d
                    JOIN rooms r ON r.id = d.room_id
                    WHERE r.environment_id = @envId
                      AND d.serial_number = ANY(@serials)
                ) asset_rooms
                GROUP BY asset_rooms.room_id
                ORDER BY COUNT(*) DESC, asset_rooms.room_id
                LIMIT 1
                """,
                new { envId, serials = CreateDemoSlotSerials(demoSlot.Value) },
                transaction);

            if (roomIdByAssets.HasValue)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE rooms
                    SET icon = @icon
                    WHERE id = @roomId
                      AND icon IN ('factory', 'home')
                    """,
                    new { roomId = roomIdByAssets.Value, icon },
                    transaction);
                return roomIdByAssets.Value;
            }
        }

        var roomId = await connection.QuerySingleAsync<int>(
            """
            WITH inserted AS (
                INSERT INTO rooms(name, environment_id, icon)
                SELECT @name, @envId, @icon
                WHERE NOT EXISTS (
                    SELECT 1 FROM rooms WHERE environment_id = @envId AND name = @name
                )
                RETURNING id
            )
            SELECT id FROM inserted
            UNION ALL
            SELECT id FROM rooms WHERE environment_id = @envId AND name = @name ORDER BY id LIMIT 1
            """,
            new { envId, name, icon },
            transaction);

        await connection.ExecuteAsync(
            """
            UPDATE rooms
            SET icon = @icon
            WHERE id = @roomId
              AND icon IN ('factory', 'home')
            """,
            new { roomId, icon },
            transaction);

        return roomId;
    }

    private async Task EnsureSensorAsync(string serialNumber, int typeId, int roomId, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO sensors(serial_number, type_id, room_id, secret)
            SELECT @serialNumber, @typeId, @roomId, @secret
            WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE serial_number = @serialNumber)
            """,
            new { serialNumber, typeId, roomId, secret = Md5(serialNumber + serialNumber) },
            transaction);

        await connection.ExecuteAsync(
            "UPDATE sensors SET type_id = @typeId, room_id = @roomId WHERE serial_number = @serialNumber",
            new { serialNumber, typeId, roomId },
            transaction);
    }

    private async Task EnsureDeviceAsync(string serialNumber, int roomId, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO devices(serial_number, room_id, secret)
            SELECT @serialNumber, @roomId, @secret
            WHERE NOT EXISTS (SELECT 1 FROM devices WHERE serial_number = @serialNumber)
            """,
            new { serialNumber, roomId, secret = Md5(serialNumber + serialNumber) },
            transaction);

        await connection.ExecuteAsync(
            "UPDATE devices SET room_id = @roomId WHERE serial_number = @serialNumber",
            new { serialNumber, roomId },
            transaction);
    }

    private async Task EnsureCo2CurveAsync(int roomId, int co2ParameterId, IDbTransaction transaction)
    {
        const string curve = """
                             {"CriticalValue":1400,"Points":[{"Value":400,"FanSpeed":0},{"Value":900,"FanSpeed":35},{"Value":1400,"FanSpeed":75},{"Value":2000,"FanSpeed":100}]}
                             """;
        await connection.ExecuteAsync(
            """
            INSERT INTO settings(room_id, parameter_id, curve)
            VALUES (@roomId, @co2ParameterId, CAST(@curve AS json))
            ON CONFLICT (room_id, parameter_id) DO NOTHING
            """,
            new { roomId, co2ParameterId, curve },
            transaction);
    }

    private async Task EnsureProfileAsync(int roomId, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO demo_room_profiles(room_id, scenario)
            VALUES (@roomId, 'auto')
            ON CONFLICT (room_id) DO NOTHING
            """,
            new { roomId },
            transaction);
    }

    private async Task SyncDemoRoomLayoutAssetsAsync(int roomId, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            WITH room_scope AS (
                SELECT r.id AS room_id,
                       r.layout,
                       row_number() OVER (ORDER BY r.id) AS demo_index
                FROM rooms r
                JOIN environments e ON e.id = r.environment_id
                WHERE e.name = @demoEnvironmentName
            ),
            room_profiles AS (
                SELECT rs.room_id,
                       rs.layout,
                       (mod((rs.demo_index - 1), 4) + 1)::int AS profile_index
                FROM room_scope rs
                WHERE rs.room_id = @roomId
            ),
            demo_rooms AS (
                SELECT rp.room_id,
                       rp.layout,
                       rp.profile_index,
                       CASE rp.profile_index
                           WHEN 1 THEN 18::numeric
                           WHEN 2 THEN 10::numeric
                           WHEN 3 THEN 12::numeric
                           ELSE 14::numeric
                       END AS width,
                       CASE rp.profile_index
                           WHEN 1 THEN 9::numeric
                           WHEN 2 THEN 7::numeric
                           WHEN 3 THEN 8::numeric
                           ELSE 7::numeric
                       END AS height,
                       'm' AS unit,
                       CASE rp.profile_index
                           WHEN 1 THEN '{"type":"rectangle","points":[{"x":0,"y":0},{"x":18,"y":0},{"x":18,"y":9},{"x":0,"y":9}]}'::jsonb
                           WHEN 2 THEN '{"type":"l_shape","points":[{"x":0,"y":0},{"x":10,"y":0},{"x":10,"y":4.2},{"x":6.6,"y":4.2},{"x":6.6,"y":7},{"x":0,"y":7}]}'::jsonb
                           WHEN 3 THEN '{"type":"t_shape","points":[{"x":3.2,"y":0},{"x":8.8,"y":0},{"x":8.8,"y":2.2},{"x":12,"y":2.2},{"x":12,"y":5.8},{"x":8.8,"y":5.8},{"x":8.8,"y":8},{"x":3.2,"y":8},{"x":3.2,"y":5.8},{"x":0,"y":5.8},{"x":0,"y":2.2},{"x":3.2,"y":2.2}]}'::jsonb
                           ELSE '{"type":"custom","points":[{"x":0,"y":0},{"x":14,"y":0},{"x":14,"y":5.6},{"x":11.8,"y":5.6},{"x":11.8,"y":7},{"x":2.1,"y":7},{"x":2.1,"y":5.9},{"x":0,"y":5.9}]}'::jsonb
                       END AS geometry,
                       CASE
                           WHEN jsonb_typeof(rp.layout -> 'items') = 'array' THEN rp.layout -> 'items'
                           ELSE '[]'::jsonb
                       END AS items
                FROM room_profiles rp
            ),
            current_items AS (
                SELECT dr.room_id,
                       item.value AS item,
                       item.ordinality
                FROM demo_rooms dr
                LEFT JOIN LATERAL jsonb_array_elements(dr.items) WITH ORDINALITY AS item(value, ordinality) ON TRUE
            ),
            preserved_items AS (
                SELECT room_id,
                       COALESCE(
                           jsonb_agg(item ORDER BY ordinality)
                               FILTER (
                                   WHERE item IS NOT NULL
                                     AND COALESCE(lower(item ->> 'type'), '') NOT IN ('sensor', 'vent')
                                     AND COALESCE(item ->> 'demo_template_item', 'false') <> 'true'
                                     AND NOT (COALESCE(item ->> 'id', '') = ANY(ARRAY[
                                         'door-main', 'door-service', 'door-lab',
                                         'window-north', 'window-east', 'window-strip',
                                         'operator-zone', 'meeting-zone', 'airlock-zone', 'maintenance-zone',
                                         'machine-press', 'machine-furnace', 'machine-compressor',
                                         'desk-row', 'printer-station', 'storage-shelves',
                                         'lab-bench-a', 'lab-bench-b', 'rack-cold', 'chemical-cabinet',
                                         'rack-east', 'rack-west', 'packing-line', 'obstacle-column'
                                     ]::text[]))
                               ),
                           '[]'::jsonb
                       ) AS items
                FROM current_items
                GROUP BY room_id
            ),
            template_items AS (
                SELECT dr.room_id,
                       COALESCE(jsonb_agg(profile_item.item ORDER BY profile_item.sort_key), '[]'::jsonb) AS items
                FROM demo_rooms dr
                CROSS JOIN LATERAL (
                    SELECT *
                    FROM (VALUES
                        (1, 10, jsonb_build_object('id', 'door-main', 'type', 'door', 'label', 'Service Door', 'x', -0.54, 'y', 4.94, 'width', 1.4, 'height', 0.32, 'rotation', -90, 'demo_template_item', true)),
                        (1, 20, jsonb_build_object('id', 'window-north', 'type', 'window', 'label', 'High Window', 'x', 5.8, 'y', 0.0, 'width', 3.0, 'height', 0.24, 'rotation', 0, 'demo_template_item', true)),
                        (1, 30, jsonb_build_object('id', 'operator-zone', 'type', 'zone', 'label', 'Operator Shift Zone', 'x', 1.2, 'y', 7.75, 'width', 4.2, 'height', 1.0, 'rotation', 0, 'demo_template_item', true)),
                        (1, 40, jsonb_build_object('id', 'machine-press', 'type', 'equipment', 'label', 'CNC Press #1', 'x', 2.6, 'y', 1.6, 'width', 3.0, 'height', 1.6, 'rotation', 0, 'heat_load_kw', 18.0, 'thermal_load', 'high', 'demo_template_item', true)),
                        (1, 50, jsonb_build_object('id', 'machine-furnace', 'type', 'equipment', 'label', 'Heat Treatment Furnace', 'x', 7.2, 'y', 4.2, 'width', 3.2, 'height', 1.8, 'rotation', 0, 'heat_load_kw', 32.0, 'thermal_load', 'high', 'demo_template_item', true)),
                        (1, 60, jsonb_build_object('id', 'machine-compressor', 'type', 'equipment', 'label', 'Compressor Station', 'x', 12.3, 'y', 5.85, 'width', 2.4, 'height', 1.5, 'rotation', -8, 'heat_load_kw', 14.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                        (2, 10, jsonb_build_object('id', 'door-main', 'type', 'door', 'label', 'Office Entry', 'x', -0.45, 'y', 5.45, 'width', 1.2, 'height', 0.3, 'rotation', -90, 'demo_template_item', true)),
                        (2, 20, jsonb_build_object('id', 'window-east', 'type', 'window', 'label', 'Facade Window', 'x', 8.1, 'y', 0.0, 'width', 1.5, 'height', 0.22, 'rotation', 0, 'demo_template_item', true)),
                        (2, 30, jsonb_build_object('id', 'desk-row', 'type', 'furniture', 'label', 'Desk Row', 'x', 1.0, 'y', 1.0, 'width', 3.3, 'height', 1.25, 'rotation', 0, 'demo_template_item', true)),
                        (2, 40, jsonb_build_object('id', 'meeting-zone', 'type', 'zone', 'label', 'Meeting Zone', 'x', 1.0, 'y', 5.05, 'width', 4.7, 'height', 1.35, 'rotation', 0, 'demo_template_item', true)),
                        (2, 50, jsonb_build_object('id', 'printer-station', 'type', 'equipment', 'label', 'Printer Station', 'x', 7.25, 'y', 1.1, 'width', 1.1, 'height', 0.8, 'rotation', 0, 'heat_load_kw', 2.5, 'thermal_load', 'low', 'demo_template_item', true)),
                        (2, 60, jsonb_build_object('id', 'storage-shelves', 'type', 'obstacle', 'label', 'Storage Shelves', 'x', 5.0, 'y', 2.55, 'width', 1.2, 'height', 1.05, 'rotation', 0, 'demo_template_item', true)),
                        (3, 10, jsonb_build_object('id', 'door-lab', 'type', 'door', 'label', 'Lab Entry', 'x', 5.4, 'y', 7.72, 'width', 1.2, 'height', 0.28, 'rotation', 180, 'demo_template_item', true)),
                        (3, 20, jsonb_build_object('id', 'airlock-zone', 'type', 'zone', 'label', 'Airlock Zone', 'x', 4.6, 'y', 5.9, 'width', 2.8, 'height', 1.15, 'rotation', 0, 'demo_template_item', true)),
                        (3, 30, jsonb_build_object('id', 'lab-bench-a', 'type', 'equipment', 'label', 'Process Bench A', 'x', 1.0, 'y', 3.0, 'width', 2.0, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 5.0, 'thermal_load', 'low', 'demo_template_item', true)),
                        (3, 40, jsonb_build_object('id', 'lab-bench-b', 'type', 'equipment', 'label', 'Process Bench B', 'x', 9.0, 'y', 3.0, 'width', 2.0, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 7.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                        (3, 50, jsonb_build_object('id', 'rack-cold', 'type', 'equipment', 'label', 'Cold Storage Rack', 'x', 4.05, 'y', 0.55, 'width', 3.9, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 3.5, 'thermal_load', 'low', 'demo_template_item', true)),
                        (3, 60, jsonb_build_object('id', 'chemical-cabinet', 'type', 'obstacle', 'label', 'Chemical Cabinet', 'x', 4.25, 'y', 3.45, 'width', 1.2, 'height', 0.9, 'rotation', 0, 'demo_template_item', true)),
                        (4, 10, jsonb_build_object('id', 'door-service', 'type', 'door', 'label', 'Service Door', 'x', -0.48, 'y', 3.18, 'width', 1.25, 'height', 0.3, 'rotation', -90, 'demo_template_item', true)),
                        (4, 20, jsonb_build_object('id', 'window-strip', 'type', 'window', 'label', 'Inspection Window', 'x', 4.2, 'y', 0.0, 'width', 2.2, 'height', 0.22, 'rotation', 0, 'demo_template_item', true)),
                        (4, 30, jsonb_build_object('id', 'rack-east', 'type', 'equipment', 'label', 'Server Rack East', 'x', 9.9, 'y', 0.9, 'width', 1.2, 'height', 3.0, 'rotation', 0, 'heat_load_kw', 9.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                        (4, 40, jsonb_build_object('id', 'rack-west', 'type', 'equipment', 'label', 'Server Rack West', 'x', 3.0, 'y', 0.9, 'width', 1.2, 'height', 3.0, 'rotation', 0, 'heat_load_kw', 8.5, 'thermal_load', 'medium', 'demo_template_item', true)),
                        (4, 50, jsonb_build_object('id', 'packing-line', 'type', 'equipment', 'label', 'Packing Line', 'x', 5.3, 'y', 4.6, 'width', 4.4, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 11.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                        (4, 60, jsonb_build_object('id', 'maintenance-zone', 'type', 'zone', 'label', 'Maintenance Zone', 'x', 1.0, 'y', 4.35, 'width', 3.0, 'height', 1.15, 'rotation', 0, 'demo_template_item', true)),
                        (4, 70, jsonb_build_object('id', 'obstacle-column', 'type', 'obstacle', 'label', 'Structural Column', 'x', 6.65, 'y', 2.4, 'width', 0.55, 'height', 0.55, 'rotation', 0, 'demo_template_item', true))
                    ) AS profile_item(profile_index, sort_key, item)
                    WHERE profile_item.profile_index = dr.profile_index
                ) profile_item
                GROUP BY dr.room_id
            ),
            ranked_sensors AS (
                SELECT dr.room_id,
                       dr.width,
                       dr.height,
                       dr.profile_index,
                       s.id,
                       s.serial_number,
                       row_number() OVER (PARTITION BY dr.room_id ORDER BY s.id) AS rn
                FROM demo_rooms dr
                JOIN sensors s ON s.room_id = dr.room_id
            ),
            ranked_devices AS (
                SELECT dr.room_id,
                       dr.width,
                       dr.height,
                       dr.profile_index,
                       d.id,
                       d.serial_number,
                       row_number() OVER (PARTITION BY dr.room_id ORDER BY d.id) AS rn
                FROM demo_rooms dr
                JOIN devices d ON d.room_id = dr.room_id
            ),
            sensor_defaults AS (
                SELECT s.*,
                       CASE s.rn
                           WHEN 1 THEN CASE s.profile_index WHEN 1 THEN 'S1 Press Zone' WHEN 2 THEN 'S1 Supply Zone' WHEN 3 THEN 'S1 Left Bench' ELSE 'S1 Rack Intake' END
                           WHEN 2 THEN CASE s.profile_index WHEN 1 THEN 'S2 Furnace Zone' WHEN 2 THEN 'S2 Meeting Zone' WHEN 3 THEN 'S2 Center Cross' ELSE 'S2 Line Center' END
                           WHEN 3 THEN CASE s.profile_index WHEN 1 THEN 'S3 Exhaust Zone' WHEN 2 THEN 'S3 Return Zone' WHEN 3 THEN 'S3 Right Bench' ELSE 'S3 Exhaust Zone' END
                           ELSE 'Sensor #' || s.id
                       END AS default_label,
                       CASE s.profile_index
                           WHEN 1 THEN CASE s.rn WHEN 1 THEN 2.92 WHEN 2 THEN 8.32 WHEN 3 THEN 14.32 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (((s.rn - 1) % 5)::numeric * 2.4))) END
                           WHEN 2 THEN CASE s.rn WHEN 1 THEN 1.2 WHEN 2 THEN 5.25 WHEN 3 THEN 8.45 ELSE greatest(0::numeric, least(s.width - 0.56, 0.9 + (((s.rn - 1) % 4)::numeric * 1.6))) END
                           WHEN 3 THEN CASE s.rn WHEN 1 THEN 1.1 WHEN 2 THEN 5.72 WHEN 3 THEN 10.05 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (((s.rn - 1) % 4)::numeric * 2.1))) END
                           ELSE CASE s.rn WHEN 1 THEN 2.0 WHEN 2 THEN 6.5 WHEN 3 THEN 11.6 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (((s.rn - 1) % 4)::numeric * 2.4))) END
                       END AS default_x,
                       CASE s.profile_index
                           WHEN 1 THEN CASE s.rn WHEN 1 THEN 4.02 WHEN 2 THEN 6.62 WHEN 3 THEN 4.12 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 5)::numeric * 1.2))) END
                           WHEN 2 THEN CASE s.rn WHEN 1 THEN 2.55 WHEN 2 THEN 5.75 WHEN 3 THEN 2.75 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.1))) END
                           WHEN 3 THEN CASE s.rn WHEN 1 THEN 3.42 WHEN 2 THEN 1.0 WHEN 3 THEN 3.42 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.2))) END
                           ELSE CASE s.rn WHEN 1 THEN 1.4 WHEN 2 THEN 4.7 WHEN 3 THEN 2.2 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.1))) END
                       END AS default_y
                FROM ranked_sensors s
            ),
            device_defaults AS (
                SELECT d.*,
                       CASE WHEN d.rn % 2 = 0 THEN 'exhaust' ELSE 'supply' END AS default_airflow_role,
                       CASE d.rn
                           WHEN 1 THEN 'V1 Supply Fan'
                           WHEN 2 THEN 'V2 Extract Fan'
                           ELSE 'Vent #' || d.id
                       END AS default_label,
                       CASE d.profile_index
                           WHEN 1 THEN CASE d.rn WHEN 1 THEN 16.95 WHEN 2 THEN 16.95 WHEN 3 THEN 0.25 WHEN 4 THEN 0.25 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (((d.rn - 1) % 3)::numeric * 1.0))) END
                           WHEN 2 THEN CASE d.rn WHEN 1 THEN 0.25 WHEN 2 THEN 8.95 WHEN 3 THEN 3.1 WHEN 4 THEN 6.0 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (((d.rn - 1) % 3)::numeric * 1.0))) END
                           WHEN 3 THEN CASE d.rn WHEN 1 THEN 5.6 WHEN 2 THEN 5.6 WHEN 3 THEN 0.25 WHEN 4 THEN 10.95 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (((d.rn - 1) % 3)::numeric * 1.0))) END
                           ELSE CASE d.rn WHEN 1 THEN 0.3 WHEN 2 THEN 12.9 WHEN 3 THEN 6.7 WHEN 4 THEN 10.7 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (((d.rn - 1) % 3)::numeric * 1.0))) END
                       END AS default_x,
                       CASE d.profile_index
                           WHEN 1 THEN CASE d.rn WHEN 1 THEN 1.25 WHEN 2 THEN 7.7 WHEN 3 THEN 1.0 WHEN 4 THEN 7.2 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                           WHEN 2 THEN CASE d.rn WHEN 1 THEN 1.0 WHEN 2 THEN 3.1 WHEN 3 THEN 6.05 WHEN 4 THEN 0.25 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                           WHEN 3 THEN CASE d.rn WHEN 1 THEN 7.05 WHEN 2 THEN 0.2 WHEN 3 THEN 3.2 WHEN 4 THEN 3.2 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                           ELSE CASE d.rn WHEN 1 THEN 2.6 WHEN 2 THEN 1.2 WHEN 3 THEN 6.1 WHEN 4 THEN 5.05 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                       END AS default_y,
                       CASE d.profile_index
                           WHEN 1 THEN CASE d.rn WHEN 1 THEN 180 WHEN 2 THEN 180 WHEN 3 THEN 0 WHEN 4 THEN 0 ELSE 180 END
                           WHEN 2 THEN CASE d.rn WHEN 1 THEN 0 WHEN 2 THEN 180 WHEN 3 THEN 270 WHEN 4 THEN 90 ELSE 180 END
                           WHEN 3 THEN CASE d.rn WHEN 1 THEN 270 WHEN 2 THEN 90 WHEN 3 THEN 0 WHEN 4 THEN 180 ELSE 180 END
                           ELSE CASE d.rn WHEN 1 THEN 0 WHEN 2 THEN 180 WHEN 3 THEN 270 WHEN 4 THEN 180 ELSE 180 END
                       END AS default_rotation
                FROM ranked_devices d
            ),
            existing_sensor_items AS (
                SELECT s.room_id,
                       10 AS sort_group,
                       ci.ordinality AS sort_key,
                       (ci.item - 'device_id') || jsonb_build_object(
                           'id', 'sensor-' || s.id,
                           'type', 'sensor',
                           'label', s.default_label,
                           'sensor_id', s.id,
                           'serial_number', s.serial_number,
                           'x', round(s.default_x, 2),
                           'y', round(s.default_y, 2),
                           'width', 0.56,
                           'height', 0.56,
                           'rotation', 0
                       ) AS item
                FROM sensor_defaults s
                JOIN LATERAL (
                    SELECT item, ordinality
                    FROM current_items ci
                    WHERE ci.room_id = s.room_id
                      AND lower(ci.item ->> 'type') = 'sensor'
                      AND (ci.item ->> 'sensor_id') ~ '^[0-9]+$'
                      AND (ci.item ->> 'sensor_id')::int = s.id
                    ORDER BY ordinality
                    LIMIT 1
                ) ci ON TRUE
            ),
            missing_sensor_items AS (
                SELECT s.room_id,
                       10 AS sort_group,
                       s.rn + 10000 AS sort_key,
                       jsonb_build_object(
                           'id', 'sensor-' || s.id,
                           'type', 'sensor',
                           'label', s.default_label,
                           'sensor_id', s.id,
                           'serial_number', s.serial_number,
                           'x', round(s.default_x, 2),
                           'y', round(s.default_y, 2),
                           'width', 0.56,
                           'height', 0.56,
                           'rotation', 0
                       ) AS item
                FROM sensor_defaults s
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM existing_sensor_items existing
                    WHERE existing.room_id = s.room_id
                      AND (existing.item ->> 'sensor_id')::int = s.id
                )
            ),
            existing_device_items AS (
                SELECT d.room_id,
                       20 AS sort_group,
                       ci.ordinality AS sort_key,
                       (ci.item - 'sensor_id') || jsonb_build_object(
                           'id', 'vent-' || d.id,
                           'type', 'vent',
                           'label', d.default_label,
                           'device_id', d.id,
                           'serial_number', d.serial_number,
                           'airflow_role', d.default_airflow_role,
                           'x', round(d.default_x, 2),
                           'y', round(d.default_y, 2),
                           'width', 0.8,
                           'height', 0.8,
                           'rotation', d.default_rotation
                       ) AS item
                FROM device_defaults d
                JOIN LATERAL (
                    SELECT item, ordinality
                    FROM current_items ci
                    WHERE ci.room_id = d.room_id
                      AND lower(ci.item ->> 'type') = 'vent'
                      AND (ci.item ->> 'device_id') ~ '^[0-9]+$'
                      AND (ci.item ->> 'device_id')::int = d.id
                    ORDER BY ordinality
                    LIMIT 1
                ) ci ON TRUE
            ),
            missing_device_items AS (
                SELECT d.room_id,
                       20 AS sort_group,
                       d.rn + 10000 AS sort_key,
                       jsonb_build_object(
                           'id', 'vent-' || d.id,
                           'type', 'vent',
                           'label', d.default_label,
                           'device_id', d.id,
                           'serial_number', d.serial_number,
                           'airflow_role', d.default_airflow_role,
                           'x', round(d.default_x, 2),
                           'y', round(d.default_y, 2),
                           'width', 0.8,
                           'height', 0.8,
                           'rotation', d.default_rotation
                       ) AS item
                FROM device_defaults d
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM existing_device_items existing
                    WHERE existing.room_id = d.room_id
                      AND (existing.item ->> 'device_id')::int = d.id
                )
            ),
            asset_items AS (
                SELECT room_id, sort_group, sort_key, item FROM existing_sensor_items
                UNION ALL
                SELECT room_id, sort_group, sort_key, item FROM missing_sensor_items
                UNION ALL
                SELECT room_id, sort_group, sort_key, item FROM existing_device_items
                UNION ALL
                SELECT room_id, sort_group, sort_key, item FROM missing_device_items
            ),
            generated_items AS (
                SELECT room_id,
                       COALESCE(jsonb_agg(item ORDER BY sort_group, sort_key), '[]'::jsonb) AS items
                FROM asset_items
                GROUP BY room_id
            ),
            next_layouts AS (
                SELECT dr.room_id,
                       jsonb_build_object(
                           'width', dr.width,
                           'height', dr.height,
                           'unit', dr.unit,
                           'geometry', dr.geometry,
                           'demo_template_version', 'rich-demo-v2',
                           'items', COALESCE(t.items, '[]'::jsonb) || COALESCE(p.items, '[]'::jsonb) || COALESCE(g.items, '[]'::jsonb)
                       ) AS layout
                FROM demo_rooms dr
                LEFT JOIN template_items t ON t.room_id = dr.room_id
                LEFT JOIN preserved_items p ON p.room_id = dr.room_id
                LEFT JOIN generated_items g ON g.room_id = dr.room_id
            )
            UPDATE rooms r
            SET layout = next_layouts.layout
            FROM next_layouts
            WHERE r.id = next_layouts.room_id
              AND r.layout IS DISTINCT FROM next_layouts.layout
            """,
            new { roomId, demoEnvironmentName = DemoEnvironmentName },
            transaction);
    }

    private async Task<bool> IsDemoRoomAsync(int roomId)
    {
        return await connection.QuerySingleAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM rooms r
                JOIN environments e ON e.id = r.environment_id
                WHERE r.id = @roomId AND e.name = @name
            )
            """,
            new { roomId, name = DemoEnvironmentName });
    }

    private async Task InsertSensorValueAsync(int sensorId, int parameterId, double value, DateTime timestamp, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO sensor_data(sensor_id, parameter_id, timestamp, value, sent_at)
            VALUES (@sensorId, @parameterId, @timestamp, @value, @timestamp)
            """,
            new { sensorId, parameterId, timestamp, value = Math.Round(value, 2) },
            transaction);
    }

    private static DemoBackfillState CreateInitialBackfillState(int roomId) => new()
    {
        Co2 = 520 + roomId * 12,
        Temperature = 21.5 + roomId % 3,
        Humidity = 42 + roomId % 8,
        Occupancy = roomId % 5,
        VentilationPower = 24
    };

    private static string ChooseScenario(int roomId, DateTime timestamp)
    {
        var index = (timestamp.Hour / 2 + roomId) % (Scenarios.Length - 1);
        return Scenarios[index + 1];
    }

    private static void ApplyScenario(
        DemoBackfillState state,
        string scenario,
        DemoBackfillRoomDto room,
        int intervalMinutes,
        Random random)
    {
        var (targetOccupancy, targetPower) = ScenarioTargets(scenario, random);
        if (room.OccupancyOverride.HasValue)
            targetOccupancy = room.OccupancyOverride.Value;
        if (room.VentilationPowerOverride.HasValue)
            targetPower = room.VentilationPowerOverride.Value;

        state.Occupancy += Math.Sign(targetOccupancy - state.Occupancy) * Math.Min(3, Math.Abs(targetOccupancy - state.Occupancy));
        state.VentilationPower += (targetPower - state.VentilationPower) * 0.35 + Noise(random, 1.1);

        if (scenario == "ventilation_failure")
            state.VentilationPower = Math.Min(state.VentilationPower, 10);

        var minutes = intervalMinutes;
        state.Co2 += state.Occupancy * 18.0 * minutes - state.VentilationPower * 7.5 * minutes - (state.Co2 - 420) * 0.025 * minutes;
        if (scenario == "critical_co2_event")
            state.Co2 += 18.0 * minutes;

        state.Temperature += state.Occupancy * 0.018 * minutes - state.VentilationPower * 0.006 * minutes + Noise(random, 0.04);
        state.Humidity += state.Occupancy * 0.03 * minutes - state.VentilationPower * 0.018 * minutes + Noise(random, 0.14);

        state.Co2 = Clamp(state.Co2, 410, 3200);
        state.Temperature = Clamp(state.Temperature, 17, 32);
        state.Humidity = Clamp(state.Humidity, 25, 85);
        state.VentilationPower = Clamp(state.VentilationPower, 0, 100);
    }

    private static (int Occupancy, double VentilationPower) ScenarioTargets(string scenario, Random random) => scenario switch
    {
        "empty_room" => (0, 18),
        "crowded_room" => (random.Next(12, 27), 65),
        "ventilation_failure" => (random.Next(8, 18), 5),
        "night_mode" => (random.Next(0, 3), 12),
        "critical_co2_event" => (random.Next(24, 40), 45),
        _ => (random.Next(2, 8), 35)
    };

    private static double Noise(Random random, double amplitude) => (random.NextDouble() * 2 - 1) * amplitude;

    private static double Clamp(double value, double lower, double upper) => Math.Max(lower, Math.Min(upper, value));

    private static string Md5(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void EnsureConnectionOpen()
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();
    }
}

public class DemoBootstrapRequest
{
    public int? RoomCount { get; set; }
}

public class DemoBackfillRequest
{
    public int? Hours { get; set; }
    public int? IntervalMinutes { get; set; }
    public string? Scenario { get; set; }
}

public class DemoRoomCreateRequest
{
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public int? SensorCount { get; set; }
    public int? DeviceCount { get; set; }
    public DemoRoomReadingsRequest? Readings { get; set; }
}

public class DemoRoomUpdateRequest
{
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
}

public class DemoRoomAssetsRequest
{
    public int? SensorCount { get; set; }
    public int? DeviceCount { get; set; }
}

public class DemoRoomReadingsRequest
{
    public double? Co2 { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? VentilationPower { get; set; }
}

public class DemoRoomProfileRequest
{
    public string Scenario { get; set; } = "auto";
    public double? VentilationPowerOverride { get; set; }
    public int? OccupancyOverride { get; set; }
}

public class DemoDataStatusDto
{
    public DemoEnvironmentDto? Environment { get; set; }
    public DemoMetricsDto Metrics { get; set; } = new();
    public List<DemoRoomStatusDto> Rooms { get; set; } = [];
    public IReadOnlyCollection<string> Scenarios { get; set; } = [];
}

public class DemoEnvironmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
}

public class DemoMetricsDto
{
    public int RoomCount { get; set; }
    public int SensorCount { get; set; }
    public int DeviceCount { get; set; }
    public int SensorDataRows { get; set; }
    public int DeviceDataRows { get; set; }
    public DateTime? LatestTelemetryAt { get; set; }
    public DateTime? LatestDeviceAt { get; set; }
}

public class DemoRoomStatusDto
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public string RoomIcon { get; set; } = "";
    public int SensorCount { get; set; }
    public string? SensorSerialNumber { get; set; }
    public string? SensorSerialNumbers { get; set; }
    public int DeviceCount { get; set; }
    public string? DeviceSerialNumber { get; set; }
    public string? DeviceSerialNumbers { get; set; }
    public string Scenario { get; set; } = "auto";
    public double? VentilationPowerOverride { get; set; }
    public int? OccupancyOverride { get; set; }
    public double? Co2 { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? VentilationPower { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public List<DemoEmitterDto> Emitters { get; set; } = [];
}

public class DemoEmitterDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double? HeatLoadKw { get; set; }
    public string? ThermalLoad { get; set; }
}

public class DemoEmitterUpdateRequest
{
    public double? HeatLoadKw { get; set; }
}

public class DemoResetResultDto
{
    public int DeletedSensorRows { get; set; }
    public int DeletedDeviceRows { get; set; }
}

public class DemoBackfillResultDto
{
    public int InsertedSensorRows { get; set; }
    public int InsertedDeviceRows { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class DemoBackfillRoomDto
{
    public int RoomId { get; set; }
    public string Scenario { get; set; } = "auto";
    public double? VentilationPowerOverride { get; set; }
    public int? OccupancyOverride { get; set; }
}

public class DemoRoomAssetIdDto
{
    public int RoomId { get; set; }
    public int AssetId { get; set; }
}

public class DemoRoomAssetLiveDto
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = "";
}

public class DemoTopologyContext
{
    public int EnvironmentId { get; set; }
    public int SensorTypeId { get; set; }
    public Dictionary<string, int> ParameterIds { get; set; } = [];
}

public class DemoReadingsApplyResultDto
{
    public int SensorRows { get; set; }
    public int DeviceRows { get; set; }
}

public class DemoBackfillState
{
    public double Co2 { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public int Occupancy { get; set; }
    public double VentilationPower { get; set; }
}
