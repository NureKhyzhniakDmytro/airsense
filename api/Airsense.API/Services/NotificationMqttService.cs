using System.Text;
using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Repository;
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
        if (payload is null || (payload.DeviceTokens.Count == 0 && payload.RecipientUserIds.Count == 0)) return;
        using var scope = GetServiceProvider().CreateScope();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var recipientUserIds = payload.RecipientUserIds.Distinct().ToList();
        if (recipientUserIds.Count == 0 && payload.DeviceTokens.Count > 0)
            recipientUserIds = (await userRepository.GetIdsByNotificationTokensAsync(payload.DeviceTokens)).Distinct().ToList();

        var notifications = await notificationRepository.AddAsync(recipientUserIds, payload.Title, payload.Body, payload.Severity, payload.Data);
        foreach (var notification in notifications)
        {
            await PublishAsync($"airsense/notifications/live/{notification.UserId}", new NotificationLiveEventDto
            {
                UserId = notification.UserId,
                Notification = notification.Notification
            });
        }

        if (payload.DeviceTokens.Count > 0)
            await notificationService.SendNotificationAsync(payload.DeviceTokens, payload.Title, payload.Body, payload.Data);
    }
}
