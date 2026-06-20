using Airsense.API.Models.Dto.Notification;

namespace Airsense.API.Repository;

public interface INotificationRepository
{
    Task<ICollection<NotificationDeliveryDto>> AddAsync(
        ICollection<int> recipientUserIds,
        string title,
        string body,
        string severity,
        Dictionary<string, string>? data);

    Task<ICollection<NotificationDto>> GetAsync(int userId, int count, int skip);

    Task<int> CountAsync(int userId);

    Task<int> CountUnreadAsync(int userId);

    Task<bool> MarkReadAsync(int userId, long notificationId);

    Task<int> MarkAllReadAsync(int userId);
}
