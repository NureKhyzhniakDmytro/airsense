using System.Text;
using Airsense.API.Models.Dto.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Airsense.API.Services;

public class MqttCommandService : MqttServiceBase
{
    private readonly IRoomLiveTelemetryHub _liveHub;
    private readonly IUserNotificationHub _notificationHub;

    public MqttCommandService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<MqttServiceBase> logger,
    IOptions<JsonOptions> jsonOptions,
    MqttClientOptions mqttOptions,
    IRoomLiveTelemetryHub liveHub,
    IUserNotificationHub notificationHub)
    : base(serviceProvider, configuration, logger, jsonOptions, mqttOptions)
    {
        _liveHub = liveHub;
        _notificationHub = notificationHub;
        RegisterCallback("airsense/telemetry", OnTelemetryAcceptedAsync);
        RegisterCallback("airsense/sensor-state", OnTelemetryAcceptedAsync);
        RegisterCallback("airsense/device-state", OnDeviceStateAcceptedAsync);
        RegisterCallback("airsense/notifications/live/+", OnNotificationAcceptedAsync);
    }

    private Task OnTelemetryAcceptedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Deserialize<TelemetryEventDto>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        if (payload is null)
            return Task.CompletedTask;

        _liveHub.Publish(new RoomLiveEventDto
        {
            Type = "sensor",
            RoomId = payload.RoomId,
            SensorId = payload.SensorId,
            SensorSerialNumber = payload.SerialNumber,
            Parameter = payload.Parameter,
            Value = payload.Data.Value,
            SentAt = payload.Data.SentAt
        });

        return Task.CompletedTask;
    }

    private Task OnDeviceStateAcceptedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Deserialize<DeviceTelemetryEventDto>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        if (payload is null)
            return Task.CompletedTask;

        _liveHub.Publish(new RoomLiveEventDto
        {
            Type = "device",
            RoomId = payload.RoomId,
            DeviceId = payload.DeviceId,
            DeviceSerialNumber = payload.SerialNumber,
            FanSpeed = payload.FanSpeed,
            ActiveAt = payload.ActiveAt,
            Source = payload.Source
        });

        return Task.CompletedTask;
    }

    private Task OnNotificationAcceptedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Deserialize<NotificationLiveEventDto>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        if (payload is null || payload.UserId <= 0)
            return Task.CompletedTask;

        _notificationHub.Publish(payload.UserId, payload.Notification);
        return Task.CompletedTask;
    }
}
