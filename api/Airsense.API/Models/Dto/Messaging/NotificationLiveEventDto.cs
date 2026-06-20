using Airsense.API.Models.Dto.Notification;

namespace Airsense.API.Models.Dto.Messaging;

public class NotificationLiveEventDto
{
    public int UserId { get; set; }

    public NotificationDto Notification { get; set; } = new();
}
