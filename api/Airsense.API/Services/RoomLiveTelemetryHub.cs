using System.Collections.Concurrent;
using System.Threading.Channels;
using Airsense.API.Models.Dto.Messaging;

namespace Airsense.API.Services;

public sealed class RoomLiveTelemetryHub : IRoomLiveTelemetryHub
{
    private const int SubscriberBufferCapacity = 128;

    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<RoomLiveEventDto>>> _rooms = new();

    public RoomLiveTelemetrySubscription Subscribe(int roomId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<RoomLiveEventDto>(new BoundedChannelOptions(SubscriberBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        var subscribers = _rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<Guid, Channel<RoomLiveEventDto>>());
        subscribers[id] = channel;

        return new RoomLiveTelemetrySubscription(channel.Reader, () =>
        {
            if (!_rooms.TryGetValue(roomId, out var roomSubscribers))
                return;

            if (roomSubscribers.TryRemove(id, out var removed))
                removed.Writer.TryComplete();

            if (roomSubscribers.IsEmpty)
                _rooms.TryRemove(roomId, out _);
        });
    }

    public void Publish(RoomLiveEventDto liveEvent)
    {
        if (!_rooms.TryGetValue(liveEvent.RoomId, out var subscribers))
            return;

        foreach (var subscriber in subscribers.Values)
            subscriber.Writer.TryWrite(liveEvent);
    }
}
