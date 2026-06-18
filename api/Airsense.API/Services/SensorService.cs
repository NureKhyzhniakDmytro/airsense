using Airsense.API.Models.Dto.Sensor;
using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Models.Dto.Settings;
using Airsense.API.Repository;

namespace Airsense.API.Services;

public class SensorService(
    IDeviceRepository deviceRepository,
    IEnvironmentRepository environmentRepository,
    ISettingsRepository settingsRepository,
    IAiPredictionService aiPredictionService,
    IMqttService mqttService) : ISensorService
{
    public async Task ProcessDataAsync(int roomId, string parameter, SensorDataDto data)
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

        if (data.Value >= curve.CriticalValue)
        {
            var environment = await environmentRepository.GetByRoomIdAsync(roomId);
            if (environment is null)
                return;

            var membersTokens = await environmentRepository.GetMembersNotificationTokensAsync(environment.Id);
            if (membersTokens.Count == 0)
                return;

            await mqttService.PublishAsync("airsense/notifications", new NotificationEventDto
            {
                DeviceTokens = membersTokens,
                Title = "Critical value exceeded",
                Body = $"Critical value exceeded for {parameter} in {environment.Name}"
            });
        }
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

        return GetFanSpeedByValue(points, value);
    }
}
