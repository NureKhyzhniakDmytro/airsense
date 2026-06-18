using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("demo-data")]
[Authorize]
public class DemoDataController(IDbConnection connection) : ControllerBase
{
    private const string DemoEnvironmentName = "AirSense Demo Environment";
    private const string DemoRoomPrefix = "Demo Room";
    private const string DemoSensorLikePattern = "demo-room-%-microclimate%";
    private const string DemoDeviceLikePattern = "demo-room-%-ventilation%";

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
        var sensorCount = request?.SensorCount ?? 1;
        var deviceCount = request?.DeviceCount ?? 1;

        EnsureConnectionOpen();
        int roomId;
        using (var transaction = connection.BeginTransaction())
        {
            var context = await EnsureDemoTopologyContextAsync(transaction);
            roomId = await CreateDemoRoomAsync(context.EnvironmentId, roomName, roomIcon, transaction);
            await AddDemoAssetsAsync(roomId, context.SensorTypeId, sensorCount, deviceCount, transaction);
            await EnsureCo2CurveAsync(roomId, context.ParameterIds["co2"], transaction);
            await EnsureProfileAsync(roomId, transaction);
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
            latest_sensor AS (
                SELECT DISTINCT ON (s.room_id, p.name)
                    s.room_id,
                    p.name,
                    sd.value,
                    sd.timestamp
                FROM sensors s
                JOIN sensor_data sd ON sd.sensor_id = s.id
                JOIN parameters p ON p.id = sd.parameter_id
                WHERE s.serial_number LIKE @sensorPattern
                ORDER BY s.room_id, p.name, sd.timestamp DESC
            ),
            sensor_pivot AS (
                SELECT
                    room_id,
                    MAX(value) FILTER (WHERE name = 'co2') AS Co2,
                    MAX(value) FILTER (WHERE name = 'temperature') AS Temperature,
                    MAX(value) FILTER (WHERE name = 'humidity') AS Humidity,
                    MAX(value) FILTER (WHERE name = 'occupancy') AS Occupancy,
                    MAX(timestamp) AS LastTelemetryAt
                FROM latest_sensor
                GROUP BY room_id
            ),
            latest_device AS (
                SELECT DISTINCT ON (d.room_id)
                    d.room_id,
                    dd.value AS VentilationPower,
                    dd.timestamp AS LastDeviceAt
                FROM devices d
                JOIN device_data dd ON dd.device_id = d.id
                WHERE d.serial_number LIKE @devicePattern
                ORDER BY d.room_id, dd.timestamp DESC
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
                sp.Occupancy AS Occupancy,
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

        return new DemoDataStatusDto
        {
            Environment = environment,
            Metrics = metrics,
            Rooms = rooms.ToList(),
            Scenarios = Scenarios
        };
    }

    private async Task BootstrapInternalAsync(int roomCount)
    {
        await EnsureDemoControlSchemaAsync();
        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();

        var context = await EnsureDemoTopologyContextAsync(transaction);

        for (var index = 1; index <= roomCount; index++)
        {
            var roomId = await EnsureRoomAsync(context.EnvironmentId, $"{DemoRoomPrefix} {index}", index % 2 == 0 ? "office" : "production", transaction);
            await AddDemoAssetsAsync(roomId, context.SensorTypeId, 1, 1, transaction);
            await EnsureCo2CurveAsync(roomId, context.ParameterIds["co2"], transaction);
            await EnsureProfileAsync(roomId, transaction);
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
            new { names = new[] { "co2", "temperature", "humidity", "occupancy" } }))
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
                        await InsertSensorValueAsync(sensorId, parameterIds["occupancy"], state.Occupancy, timestamp, transaction);
                        sensorRows += 4;
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
            ["temperature"] = await EnsureParameterAsync("temperature", "C", -50, 50, transaction),
            ["humidity"] = await EnsureParameterAsync("humidity", "%", 0, 100, transaction),
            ["co2"] = await EnsureParameterAsync("co2", "ppm", 300, 5000, transaction),
            ["occupancy"] = await EnsureParameterAsync("occupancy", "people", 0, 100, transaction)
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
        IDbTransaction transaction)
    {
        var existingSensorCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sensors WHERE room_id = @roomId AND serial_number LIKE @sensorPattern",
            new { roomId, sensorPattern = DemoSensorLikePattern },
            transaction);
        var existingDeviceCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM devices WHERE room_id = @roomId AND serial_number LIKE @devicePattern",
            new { roomId, devicePattern = DemoDeviceLikePattern },
            transaction);

        for (var index = 1; index <= sensorCount; index++)
        {
            var serialIndex = existingSensorCount + index;
            await EnsureSensorAsync(CreateDemoAssetSerial(roomId, "microclimate", serialIndex), sensorTypeId, roomId, transaction);
        }

        for (var index = 1; index <= deviceCount; index++)
        {
            var serialIndex = existingDeviceCount + index;
            await EnsureDeviceAsync(CreateDemoAssetSerial(roomId, "ventilation", serialIndex), roomId, transaction);
        }
    }

    private async Task<DemoReadingsApplyResultDto> ApplyRoomReadingsInternalAsync(int roomId, DemoRoomReadingsRequest request)
    {
        var parameterIds = (await connection.QueryAsync<(int Id, string Name)>(
            "SELECT id AS Id, name AS Name FROM parameters WHERE name = ANY(@names)",
            new { names = new[] { "co2", "temperature", "humidity", "occupancy" } }))
            .ToDictionary(x => x.Name, x => x.Id);

        EnsureConnectionOpen();
        using var transaction = connection.BeginTransaction();
        var context = await EnsureDemoTopologyContextAsync(transaction);
        foreach (var parameterId in context.ParameterIds)
            parameterIds.TryAdd(parameterId.Key, parameterId.Value);

        var sensorIds = (await connection.QueryAsync<int>(
            "SELECT id FROM sensors WHERE room_id = @roomId AND serial_number LIKE @sensorPattern ORDER BY id",
            new { roomId, sensorPattern = DemoSensorLikePattern },
            transaction)).ToList();
        var deviceIds = (await connection.QueryAsync<int>(
            "SELECT id FROM devices WHERE room_id = @roomId AND serial_number LIKE @devicePattern ORDER BY id",
            new { roomId, devicePattern = DemoDeviceLikePattern },
            transaction)).ToList();

        var timestamp = DateTime.UtcNow;
        var sensorRows = 0;
        var deviceRows = 0;

        foreach (var sensorId in sensorIds)
        {
            if (request.Co2.HasValue)
            {
                await InsertSensorValueAsync(sensorId, parameterIds["co2"], request.Co2.Value, timestamp, transaction);
                sensorRows++;
            }
            if (request.Temperature.HasValue)
            {
                await InsertSensorValueAsync(sensorId, parameterIds["temperature"], request.Temperature.Value, timestamp, transaction);
                sensorRows++;
            }
            if (request.Humidity.HasValue)
            {
                await InsertSensorValueAsync(sensorId, parameterIds["humidity"], request.Humidity.Value, timestamp, transaction);
                sensorRows++;
            }
            if (request.Occupancy.HasValue)
            {
                await InsertSensorValueAsync(sensorId, parameterIds["occupancy"], request.Occupancy.Value, timestamp, transaction);
                sensorRows++;
            }
        }

        if (request.VentilationPower.HasValue)
        {
            foreach (var deviceId in deviceIds)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO device_data(device_id, timestamp, value, applied, applied_at)
                    VALUES (@deviceId, @timestamp, @value, TRUE, @timestamp)
                    """,
                    new { deviceId, timestamp, value = Math.Round(request.VentilationPower.Value, 2) },
                    transaction);
                deviceRows++;
            }
        }

        transaction.Commit();
        return new DemoReadingsApplyResultDto
        {
            SensorRows = sensorRows,
            DeviceRows = deviceRows
        };
    }

    private static string CreateDemoAssetSerial(int roomId, string suffix, int index)
    {
        var serial = $"demo-room-{roomId}-{suffix}";
        return index <= 1 ? serial : $"{serial}-{index}";
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
        if ((request?.SensorCount ?? 1) + (request?.DeviceCount ?? 1) == 0)
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
            return "Temperature must be between -50 and 50 C";
        if (request.Humidity is < 0 or > 100)
            return "Humidity must be between 0 and 100%";
        if (request.Occupancy is < 0 or > 100)
            return "Occupancy must be between 0 and 100";
        if (request.VentilationPower is < 0 or > 100)
            return "Ventilation power must be between 0 and 100%";

        return null;
    }

    private static bool HasSensorReadings(DemoRoomReadingsRequest request)
    {
        return request.Co2.HasValue
               || request.Temperature.HasValue
               || request.Humidity.HasValue
               || request.Occupancy.HasValue;
    }

    private async Task<int> EnsureParameterAsync(string name, string unit, double minValue, double maxValue, IDbTransaction transaction)
    {
        return await connection.QuerySingleAsync<int>(
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
    }

    private async Task<int> EnsureSensorTypeAsync(string name, IEnumerable<int> parameterIds, IDbTransaction transaction)
    {
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

        foreach (var parameterId in parameterIds)
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
                SELECT @name, 'factory'
                WHERE NOT EXISTS (SELECT 1 FROM environments WHERE name = @name)
                RETURNING id
            )
            SELECT id FROM inserted
            UNION ALL
            SELECT id FROM environments WHERE name = @name ORDER BY id LIMIT 1
            """,
            new { name = DemoEnvironmentName },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO environment_members(member_id, environment_id, role)
            SELECT id, @envId, 'owner'
            FROM users
            ON CONFLICT (member_id, environment_id) DO NOTHING
            """,
            new { envId },
            transaction);

        return envId;
    }

    private async Task<int> EnsureRoomAsync(int envId, string name, string icon, IDbTransaction transaction)
    {
        return await connection.QuerySingleAsync<int>(
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
    public int? Occupancy { get; set; }
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
    public double? Occupancy { get; set; }
    public double? VentilationPower { get; set; }
    public DateTime? LastActivityAt { get; set; }
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
