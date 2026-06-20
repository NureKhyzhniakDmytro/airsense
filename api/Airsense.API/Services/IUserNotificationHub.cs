using System.Threading.Channels;
using Airsense.API.Models.Dto.Notification;

namespace Airsense.API.Services;

public interface IUserNotificationHub
{
    UserNotificationSubscription Subscribe(int userId);

    void Publish(int userId, NotificationDto notification);
}

public sealed class UserNotificationSubscription : IAsyncDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public UserNotificationSubscription(ChannelReader<NotificationDto> reader, Action onDispose)
    {
        Reader = reader;
        _onDispose = onDispose;
    }

    public ChannelReader<NotificationDto> Reader { get; }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _onDispose();
        return ValueTask.CompletedTask;
    }
}
