using System.Data;
using System.Text;
using System.Text.Json;
using Airsense.API.Models.Dto.Ai;
using Airsense.API.Models.Dto.Messaging;
using Dapper;

namespace Airsense.API.Services;

public sealed class AiPredictionService(
    IDbConnection connection,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMqttService mqttService) : IAiPredictionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly List<int> DefaultHorizons = [10, 20, 30];
    private const double TargetCo2 = 900;
    private const double MaxVentilationPower = 100;
    private const int RecommendationHorizonMinutes = 20;

    private string PredictionServiceUrl =>
        configuration["Ai:PredictionServiceUrl"]?.TrimEnd('/') ?? "http://ai-prediction-service:8000";

    public async Task<RoomAiInsightsDto> GetRoomInsightsAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var sample = await LoadLatestSampleAsync(roomId);
        var recentRecommendations = await LoadRecentRecommendationsAsync(roomId);

        if (sample is null)
        {
            return new RoomAiInsightsDto
            {
                HasSample = false,
                Message = "CO2, temperature, and humidity telemetry are required before AI predictions can run.",
                RecentRecommendations = recentRecommendations
            };
        }

        var prediction = await PostAsync<AiPredictRequestDto, AiPredictResponseDto>(
            "/predict",
            new AiPredictRequestDto
            {
                Sample = sample,
                HorizonsMinutes = DefaultHorizons
            },
            cancellationToken);

        var simulation = await PostAsync<AiSimulateRequestDto, AiSimulateResponseDto>(
            "/simulate",
            new AiSimulateRequestDto
            {
                Sample = sample,
                HorizonsMinutes = DefaultHorizons,
                Scenarios = BuildScenarios(sample.VentilationPower)
            },
            cancellationToken);

        return new RoomAiInsightsDto
        {
            HasSample = true,
            TelemetryAgeSeconds = sample.Timestamp is null
                ? null
                : Math.Max(0, (DateTime.UtcNow - sample.Timestamp.Value.ToUniversalTime()).TotalSeconds),
            Sample = sample,
            Prediction = prediction,
            Simulation = simulation,
            RecentRecommendations = recentRecommendations
        };
    }

    public async Task<AiRecommendationAuditDto> CreateRoomRecommendationAsync(
        int roomId,
        CancellationToken cancellationToken = default)
    {
        var sample = await LoadLatestSampleAsync(roomId);
        if (sample is null)
            throw new InvalidOperationException("CO2, temperature, and humidity telemetry are required before AI recommendations can run.");

        var recommendation = await PostAsync<AiRecommendationRequestDto, AiRecommendationResponseDto>(
            "/recommendation",
            new AiRecommendationRequestDto
            {
                Sample = sample,
                TargetCo2 = TargetCo2,
                MaxVentilationPower = MaxVentilationPower,
                HorizonMinutes = RecommendationHorizonMinutes
            },
            cancellationToken);

        var payload = new AiRecommendationPayloadDto
        {
            ModelVersion = recommendation.ModelVersion,
            Mode = recommendation.Mode,
            Reason = recommendation.Reason,
            Sample = sample,
            Predicted = recommendation.Predicted,
            TargetCo2 = TargetCo2,
            MaxVentilationPower = MaxVentilationPower,
            HorizonMinutes = RecommendationHorizonMinutes
        };

        var inserted = await InsertRecommendationAsync(
            roomId,
            sample,
            recommendation.SuggestedVentilationPower,
            payload);

        return inserted;
    }

    public async Task<AiRecommendationAuditDto?> AcceptRecommendationAsync(
        int roomId,
        long recommendationId,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();
        var committed = false;
        try
        {
            const string selectSql = """
                                     SELECT
                                         vc.id AS Id,
                                         vc.timestamp AS Timestamp,
                                         vc.requested_power AS RequestedPower,
                                         vc.status AS Status,
                                         vc.payload::text AS PayloadJson,
                                         COALESCE(vc.device_id, room_device.id) AS DeviceId
                                     FROM ventilation_commands vc
                                     LEFT JOIN LATERAL (
                                         SELECT id
                                         FROM devices
                                         WHERE room_id = @roomId
                                         ORDER BY id
                                         LIMIT 1
                                     ) room_device ON TRUE
                                     WHERE vc.id = @recommendationId
                                       AND vc.room_id = @roomId
                                       AND vc.source = 'ai-recommendation'
                                       AND vc.command_type = 'recommendation'
                                       AND vc.status IN ('recommended', 'accepted')
                                     FOR UPDATE OF vc
                                     """;

            var row = await connection.QueryFirstOrDefaultAsync<RecommendationApplicationRow>(
                new CommandDefinition(
                    selectSql,
                    new { roomId, recommendationId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (row is null)
            {
                transaction.Rollback();
                return null;
            }

            if (row.RequestedPower is null)
                throw new InvalidOperationException("AI recommendation does not contain a ventilation power value.");

            if (row.DeviceId is null)
                throw new InvalidOperationException("Room does not have a ventilation device to apply this recommendation.");

            var requestedPower = Math.Round(Math.Clamp(row.RequestedPower.Value, 0, 100), 2);

            const string insertDeviceDataSql = """
                                               INSERT INTO device_data(device_id, value)
                                               SELECT id, @requestedPower
                                               FROM devices
                                               WHERE room_id = @roomId
                                               """;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertDeviceDataSql,
                    new { roomId, requestedPower },
                    transaction,
                    cancellationToken: cancellationToken));

            const string updateRecommendationSql = """
                                                   UPDATE ventilation_commands
                                                   SET status = 'used', device_id = @deviceId, requested_power = @requestedPower
                                                   WHERE id = @recommendationId
                                                   RETURNING
                                                       id AS Id,
                                                       timestamp AS Timestamp,
                                                       requested_power AS RequestedPower,
                                                       status AS Status,
                                                       payload::text AS PayloadJson
                                                   """;

            var updated = await connection.QuerySingleAsync<RecommendationRow>(
                new CommandDefinition(
                    updateRecommendationSql,
                    new
                    {
                        recommendationId,
                        deviceId = row.DeviceId.Value,
                        requestedPower
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();
            committed = true;

            await mqttService.PublishAsync($"room/{roomId}", new
            {
                fanSpeed = requestedPower,
                timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
                source = "ai-recommendation",
                recommendationId
            });
            await mqttService.PublishAsync("airsense/device-state", new DeviceTelemetryEventDto
            {
                RoomId = roomId,
                FanSpeed = requestedPower,
                ActiveAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                Source = "ai-recommendation"
            });

            return MapRecommendation(updated);
        }
        catch
        {
            if (!committed && transaction.Connection is not null)
                transaction.Rollback();
            throw;
        }
    }

    public async Task<AiAutomationRecommendationDto?> ConsumeAcceptedRecommendationAsync(int roomId)
    {
        const string sql = """
                           UPDATE ventilation_commands
                           SET status = 'used'
                           WHERE id = (
                               SELECT id
                               FROM ventilation_commands
                               WHERE room_id = @roomId
                                 AND source = 'ai-recommendation'
                                 AND command_type = 'recommendation'
                                 AND status = 'accepted'
                                 AND requested_power IS NOT NULL
                                 AND timestamp > NOW() - INTERVAL '30 minutes'
                               ORDER BY timestamp DESC
                               LIMIT 1
                           )
                           RETURNING
                               id AS Id,
                               requested_power AS RequestedPower
                           """;

        return await connection.QueryFirstOrDefaultAsync<AiAutomationRecommendationDto>(sql, new { roomId });
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{PredictionServiceUrl}{path}", content, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"AI prediction service returned {(int)response.StatusCode}: {responseJson}",
                null,
                response.StatusCode);
        }

        return JsonSerializer.Deserialize<TResponse>(responseJson, JsonOptions)
               ?? throw new InvalidOperationException("AI prediction service returned an empty response.");
    }

    private async Task<AiTelemetrySampleDto?> LoadLatestSampleAsync(int roomId)
    {
        const string sql = """
                           WITH latest_sensor AS (
                               SELECT DISTINCT ON (p.name, s.id)
                                   p.name AS parameter,
                                   sd.value,
                                   sd.timestamp
                               FROM sensors s
                               JOIN sensor_data sd ON sd.sensor_id = s.id
                               JOIN parameters p ON p.id = sd.parameter_id
                               WHERE s.room_id = @roomId
                                 AND p.name IN ('co2', 'temperature', 'humidity')
                               ORDER BY p.name, s.id, sd.timestamp DESC
                           ),
                           sensor_summary AS (
                               SELECT
                                   AVG(value) FILTER (WHERE parameter = 'co2') AS Co2,
                                   AVG(value) FILTER (WHERE parameter = 'temperature') AS Temperature,
                                   AVG(value) FILTER (WHERE parameter = 'humidity') AS Humidity,
                                   MAX(timestamp) AS LatestSensorAt
                               FROM latest_sensor
                           ),
                           vent_items AS (
                               SELECT
                                   (item ->> 'device_id')::int AS DeviceId,
                                   CASE
                                       WHEN lower(item ->> 'airflow_role') IN ('supply', 'exhaust') THEN lower(item ->> 'airflow_role')
                                       WHEN lower(COALESCE(item ->> 'label', '')) ~ '(extract|exhaust|return|outlet)' THEN 'exhaust'
                                       ELSE 'supply'
                                   END AS AirflowRole
                               FROM rooms r
                               CROSS JOIN LATERAL jsonb_array_elements(
                                   CASE
                                       WHEN jsonb_typeof(r.layout -> 'items') = 'array' THEN r.layout -> 'items'
                                       ELSE '[]'::jsonb
                                   END
                               ) AS item
                               WHERE r.id = @roomId
                                 AND lower(item ->> 'type') = 'vent'
                                 AND (item ->> 'device_id') ~ '^[0-9]+$'
                           ),
                           room_devices AS (
                               SELECT
                                   d.id AS DeviceId,
                                   COALESCE(
                                       vi.AirflowRole,
                                       CASE WHEN row_number() OVER (ORDER BY d.id) % 2 = 0 THEN 'exhaust' ELSE 'supply' END
                                   ) AS AirflowRole
                               FROM devices d
                               LEFT JOIN vent_items vi ON vi.DeviceId = d.id
                               WHERE d.room_id = @roomId
                           ),
                           latest_device_per_device AS (
                               SELECT DISTINCT ON (rd.DeviceId)
                                   rd.DeviceId,
                                   rd.AirflowRole,
                                   dd.value AS VentilationPower,
                                   COALESCE(dd.applied_at, dd.timestamp) AS DeviceTimestamp
                               FROM room_devices rd
                               JOIN device_data dd ON dd.device_id = rd.DeviceId
                               ORDER BY rd.DeviceId, COALESCE(dd.applied_at, dd.timestamp) DESC, dd.id DESC
                           ),
                           latest_device AS (
                               SELECT
                                   AVG(VentilationPower) AS VentilationPower,
                                   AVG(VentilationPower) FILTER (WHERE AirflowRole = 'supply') AS SupplyVentilationPower,
                                   AVG(VentilationPower) FILTER (WHERE AirflowRole = 'exhaust') AS ExhaustVentilationPower,
                                   MAX(DeviceTimestamp) AS DeviceTimestamp
                               FROM latest_device_per_device
                           )
                           SELECT
                               @roomId AS RoomId,
                               ss.Co2 AS Co2,
                               ss.Temperature AS Temperature,
                               ss.Humidity AS Humidity,
                               COALESCE(ld.VentilationPower, 0) AS VentilationPower,
                               COALESCE(ld.SupplyVentilationPower, 0) AS SupplyVentilationPower,
                               COALESCE(ld.ExhaustVentilationPower, 0) AS ExhaustVentilationPower,
                               GREATEST(
                                   COALESCE(ss.LatestSensorAt, TIMESTAMP 'epoch'),
                                   COALESCE(ld.DeviceTimestamp, TIMESTAMP 'epoch')
                               ) AS Timestamp,
                               NULL AS DeviceId
                           FROM sensor_summary ss
                           LEFT JOIN latest_device ld ON TRUE
                           """;

        var row = await connection.QueryFirstOrDefaultAsync<LatestTelemetryRow>(sql, new { roomId });
        if (row is null || row.Co2 is null || row.Temperature is null || row.Humidity is null)
            return null;

        return new AiTelemetrySampleDto
        {
            RoomId = roomId,
            Timestamp = row.Timestamp == DateTime.UnixEpoch ? null : DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
            Co2 = Math.Round(row.Co2.Value, 2),
            Temperature = Math.Round(row.Temperature.Value, 2),
            Humidity = Math.Round(row.Humidity.Value, 2),
            VentilationPower = Math.Round(Math.Clamp(row.VentilationPower, 0, 100), 2),
            SupplyVentilationPower = Math.Round(Math.Clamp(row.SupplyVentilationPower, 0, 100), 2),
            ExhaustVentilationPower = Math.Round(Math.Clamp(row.ExhaustVentilationPower, 0, 100), 2)
        };
    }

    private async Task<AiRecommendationAuditDto> InsertRecommendationAsync(
        int roomId,
        AiTelemetrySampleDto sample,
        double suggestedVentilationPower,
        AiRecommendationPayloadDto payload)
    {
        const string sql = """
                           INSERT INTO ventilation_commands(room_id, device_id, source, command_type, requested_power, payload, status)
                           VALUES (
                               @roomId,
                               (
                                   SELECT id
                                   FROM devices
                                   WHERE room_id = @roomId
                                   ORDER BY id
                                   LIMIT 1
                               ),
                               'ai-recommendation',
                               'recommendation',
                               @requestedPower,
                               CAST(@payloadJson AS jsonb),
                               'recommended'
                           )
                           RETURNING
                               id AS Id,
                               timestamp AS Timestamp,
                               requested_power AS RequestedPower,
                               status AS Status,
                               payload::text AS PayloadJson
                           """;

        var row = await connection.QuerySingleAsync<RecommendationRow>(
            sql,
            new
            {
                roomId,
                requestedPower = Math.Round(Math.Clamp(suggestedVentilationPower, 0, 100), 2),
                payloadJson = JsonSerializer.Serialize(payload, JsonOptions)
            });

        return MapRecommendation(row);
    }

    private async Task<List<AiRecommendationAuditDto>> LoadRecentRecommendationsAsync(int roomId, int count = 5)
    {
        const string sql = """
                           SELECT
                               id AS Id,
                               timestamp AS Timestamp,
                               requested_power AS RequestedPower,
                               status AS Status,
                               payload::text AS PayloadJson
                           FROM ventilation_commands
                           WHERE room_id = @roomId
                             AND source = 'ai-recommendation'
                             AND command_type = 'recommendation'
                           ORDER BY timestamp DESC
                           LIMIT @count
                           """;

        var rows = await connection.QueryAsync<RecommendationRow>(sql, new { roomId, count });
        return rows.Select(MapRecommendation).ToList();
    }

    private static AiRecommendationAuditDto MapRecommendation(RecommendationRow row)
    {
        var payload = string.IsNullOrWhiteSpace(row.PayloadJson)
            ? null
            : JsonSerializer.Deserialize<AiRecommendationPayloadDto>(row.PayloadJson, JsonOptions);

        return new AiRecommendationAuditDto
        {
            Id = row.Id,
            Timestamp = DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
            RequestedPower = row.RequestedPower,
            Status = row.Status,
            ModelVersion = payload?.ModelVersion ?? "",
            Mode = payload?.Mode ?? "",
            Reason = payload?.Reason ?? "",
            Predicted = payload?.Predicted,
            Sample = payload?.Sample
        };
    }

    private static List<AiVentilationScenarioDto> BuildScenarios(double currentPower)
    {
        var roundedCurrent = Math.Round(Math.Clamp(currentPower, 0, 100), 2);
        return new[]
            {
                new AiVentilationScenarioDto { Label = "Current", VentilationPower = roundedCurrent },
                new AiVentilationScenarioDto { Label = "Quiet", VentilationPower = 20 },
                new AiVentilationScenarioDto { Label = "Balanced", VentilationPower = 50 },
                new AiVentilationScenarioDto { Label = "Boost", VentilationPower = 80 }
            }
            .GroupBy(x => x.VentilationPower)
            .Select(x => x.First())
            .OrderBy(x => x.VentilationPower)
            .ToList();
    }

    private sealed class LatestTelemetryRow
    {
        public double? Co2 { get; init; }
        public double? Temperature { get; init; }
        public double? Humidity { get; init; }
        public double VentilationPower { get; init; }
        public double SupplyVentilationPower { get; init; }
        public double ExhaustVentilationPower { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private class RecommendationRow
    {
        public long Id { get; init; }
        public DateTime Timestamp { get; init; }
        public double? RequestedPower { get; init; }
        public string Status { get; init; } = "";
        public string PayloadJson { get; init; } = "";
    }

    private sealed class RecommendationApplicationRow : RecommendationRow
    {
        public int? DeviceId { get; init; }
    }
}
