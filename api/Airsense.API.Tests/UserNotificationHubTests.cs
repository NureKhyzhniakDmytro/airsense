using Airsense.API.Models.Dto.Notification;
using Airsense.API.Services;
using Xunit;

namespace Airsense.API.Tests;

public class UserNotificationHubTests
{
    [Fact]
    public async Task Publish_FansOutOnlyToSubscribersOfTargetUser()
    {
        var hub = new UserNotificationHub();
        await using var firstSubscriber = hub.Subscribe(7);
        await using var secondSubscriber = hub.Subscribe(7);
        await using var otherUserSubscriber = hub.Subscribe(8);

        var notification = new NotificationDto
        {
            Id = 12,
            Title = "Critical value exceeded",
            Severity = "critical"
        };

        hub.Publish(7, notification);

        Assert.Same(notification, await ReadNextAsync(firstSubscriber));
        Assert.Same(notification, await ReadNextAsync(secondSubscriber));
        Assert.False(otherUserSubscriber.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DisposedSubscriber_DoesNotReceiveFutureNotifications()
    {
        var hub = new UserNotificationHub();
        var disposedSubscriber = hub.Subscribe(7);
        await using var activeSubscriber = hub.Subscribe(7);

        await disposedSubscriber.DisposeAsync();
        var notification = new NotificationDto
        {
            Id = 21,
            Title = "Critical value resolved",
            Severity = "success"
        };

        hub.Publish(7, notification);

        Assert.False(disposedSubscriber.Reader.TryRead(out _));
        Assert.Same(notification, await ReadNextAsync(activeSubscriber));
    }

    private static async Task<NotificationDto> ReadNextAsync(UserNotificationSubscription subscription)
    {
        var readTask = subscription.Reader.ReadAsync().AsTask();
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.Same(readTask, completed);
        return await readTask;
    }
}
