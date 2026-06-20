using System.Collections.Concurrent;
using System.Threading.Channels;
using Airsense.API.Models.Dto.Notification;

namespace Airsense.API.Services;

public sealed class UserNotificationHub : IUserNotificationHub
{
    private const int SubscriberBufferCapacity = 64;

    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<NotificationDto>>> _users = new();

    public UserNotificationSubscription Subscribe(int userId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<NotificationDto>(new BoundedChannelOptions(SubscriberBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        var subscribers = _users.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<NotificationDto>>());
        subscribers[id] = channel;

        return new UserNotificationSubscription(channel.Reader, () =>
        {
            if (!_users.TryGetValue(userId, out var userSubscribers))
                return;

            if (userSubscribers.TryRemove(id, out var removed))
                removed.Writer.TryComplete();

            if (userSubscribers.IsEmpty)
                _users.TryRemove(userId, out _);
        });
    }

    public void Publish(int userId, NotificationDto notification)
    {
        if (!_users.TryGetValue(userId, out var subscribers))
            return;

        foreach (var subscriber in subscribers.Values)
            subscriber.Writer.TryWrite(notification);
    }
}
