namespace Airsense.API.Models.Dto.Notification;

public class NotificationDeliveryDto
{
    public int UserId { get; set; }

    public NotificationDto Notification { get; set; } = new();
}
