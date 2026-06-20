using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Services;
using Xunit;

namespace Airsense.API.Tests;

public class RoomLiveTelemetryHubTests
{
    [Fact]
    public async Task Publish_FansOutOnlyToSubscribersOfTargetRoom()
    {
        var hub = new RoomLiveTelemetryHub();
        await using var firstSubscriber = hub.Subscribe(7);
        await using var secondSubscriber = hub.Subscribe(7);
        await using var otherRoomSubscriber = hub.Subscribe(8);

        var liveEvent = new RoomLiveEventDto
        {
            Type = "sensor",
            RoomId = 7,
            SensorId = 12,
            Parameter = "temperature",
            Value = 22.4
        };

        hub.Publish(liveEvent);

        Assert.Same(liveEvent, await ReadNextAsync(firstSubscriber));
        Assert.Same(liveEvent, await ReadNextAsync(secondSubscriber));
        Assert.False(otherRoomSubscriber.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DisposedSubscriber_DoesNotReceiveFutureEvents()
    {
        var hub = new RoomLiveTelemetryHub();
        var disposedSubscriber = hub.Subscribe(7);
        await using var activeSubscriber = hub.Subscribe(7);

        await disposedSubscriber.DisposeAsync();
        var liveEvent = new RoomLiveEventDto
        {
            Type = "device",
            RoomId = 7,
            DeviceId = 21,
            FanSpeed = 55
        };

        hub.Publish(liveEvent);

        Assert.False(disposedSubscriber.Reader.TryRead(out _));
        Assert.Same(liveEvent, await ReadNextAsync(activeSubscriber));
    }

    private static async Task<RoomLiveEventDto> ReadNextAsync(RoomLiveTelemetrySubscription subscription)
    {
        var readTask = subscription.Reader.ReadAsync().AsTask();
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.Same(readTask, completed);
        return await readTask;
    }
}
