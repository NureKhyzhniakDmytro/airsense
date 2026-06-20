using Airsense.API.Models.Dto.Sensor;
using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Models.Dto.Settings;
using Airsense.API.Repository;

namespace Airsense.API.Services;

public class SensorService(
    IDeviceRepository deviceRepository,
    IEnvironmentRepository environmentRepository,
    ISettingsRepository settingsRepository,
    IThresholdAlertStateRepository thresholdAlertStateRepository,
    IAiPredictionService aiPredictionService,
    IMqttService mqttService) : ISensorService
{
    public async Task ProcessDataAsync(int roomId, int sensorId, string parameter, SensorDataDto data)
    {
        var curve = await settingsRepository.GetCurveAsync(roomId, parameter);

        if (curve?.Points == null || curve.Points.Count == 0)
            return;

        var fanSpeed = await GetFanSpeedAsync(roomId, curve.Points, data.Value);

        if (!fanSpeed.HasValue)
            return;

        await deviceRepository.AddDataAsync(roomId, fanSpeed.Value);
        await mqttService.PublishAsync($"room/{roomId}", new
        {
            fanSpeed,
            timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds(),
        });
        await mqttService.PublishAsync("airsense/device-state", new DeviceTelemetryEventDto
        {
            RoomId = roomId,
            FanSpeed = fanSpeed.Value,
            ActiveAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
            Source = "automation"
        });

        if (!curve.CriticalValue.HasValue)
            return;

        var transition = await thresholdAlertStateRepository.UpdateAsync(
            roomId,
            sensorId,
            parameter,
            data.Value,
            curve.CriticalValue.Value);

        if (transition == ThresholdAlertTransition.None)
            return;

        var environment = await environmentRepository.GetByRoomIdAsync(roomId);
        if (environment is null)
            return;

        var members = await environmentRepository.GetMembersNotificationTargetsAsync(environment.Id);
        if (members.Count == 0)
            return;

        var isResolved = transition == ThresholdAlertTransition.Resolved;
        await mqttService.PublishAsync("airsense/notifications", new NotificationEventDto
        {
            RecipientUserIds = members.Select(member => member.UserId).Distinct().ToList(),
            DeviceTokens = members
                .Select(member => member.NotificationToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct()
                .Cast<string>()
                .ToList(),
            Title = isResolved ? "Critical value resolved" : "Critical value exceeded",
            Body = isResolved
                ? $"{parameter} is back below the critical line in {environment.Name}"
                : $"{parameter} exceeded the critical line in {environment.Name}",
            Severity = isResolved ? "success" : "critical",
            Data = new Dictionary<string, string>
            {
                ["type"] = isResolved ? "critical_threshold_resolved" : "critical_threshold",
                ["status"] = isResolved ? "resolved" : "triggered",
                ["environment_id"] = environment.Id.ToString(),
                ["room_id"] = roomId.ToString(),
                ["sensor_id"] = sensorId.ToString(),
                ["parameter"] = parameter,
                ["value"] = data.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["critical_value"] = curve.CriticalValue.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            }
        });
    }

    private static int? GetFanSpeedByValue(ICollection<CurvePointDto> points, double value)
    {
        var sortedPoints = points.OrderBy(p => p.Value).ToList();

        if (value <= sortedPoints[0].Value)
            return sortedPoints[0].FanSpeed;

        if (value >= sortedPoints.Last().Value)
            return sortedPoints.Last().FanSpeed;

        for (var i = 0; i < sortedPoints.Count - 1; i++)
        {
            var current = sortedPoints[i];
            var next = sortedPoints[i + 1];
            if (value >= current.Value && value <= next.Value)
            {
                var interpolatedFanSpeed = current.FanSpeed + (value - current.Value) * (next.FanSpeed - current.FanSpeed) / (next.Value - current.Value);
                return (int)Math.Round(interpolatedFanSpeed);
            }
        }

        return null;
    }

    private async Task<int?> GetFanSpeedAsync(int roomId, ICollection<CurvePointDto> points, double value)
    {
        var aiRecommendation = await aiPredictionService.ConsumeAcceptedRecommendationAsync(roomId);
        if (aiRecommendation is not null)
            return (int)Math.Round(Math.Clamp(aiRecommendation.RequestedPower, 0, 100));

        var autonomousRecommendation = await aiPredictionService.GetAutonomousControlRecommendationAsync(roomId);
        if (autonomousRecommendation is not null)
            return (int)Math.Round(Math.Clamp(autonomousRecommendation.RequestedPower, 0, 100));

        return GetFanSpeedByValue(points, value);
    }
}
