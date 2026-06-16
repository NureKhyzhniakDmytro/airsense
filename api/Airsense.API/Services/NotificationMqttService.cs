using System.Text;
using Airsense.API.Models.Dto.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Airsense.API.Services;

public class NotificationMqttService : MqttServiceBase
{
    public NotificationMqttService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MqttServiceBase> logger, IOptions<JsonOptions> jsonOptions, MqttClientOptions mqttOptions)
        : base(serviceProvider, configuration, logger, jsonOptions, mqttOptions)
    {
        RegisterCallback("airsense/notifications", OnNotificationRequestedAsync);
    }

    private async Task OnNotificationRequestedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Deserialize<NotificationEventDto>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        if (payload is null || payload.DeviceTokens.Count == 0) return;
        using var scope = GetServiceProvider().CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendNotificationAsync(payload.DeviceTokens, payload.Title, payload.Body, payload.Data);
    }
}
