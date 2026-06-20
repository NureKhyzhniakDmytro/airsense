using System.Threading.Channels;
using Airsense.API.Models.Dto.Messaging;

namespace Airsense.API.Services;

public interface IRoomLiveTelemetryHub
{
    RoomLiveTelemetrySubscription Subscribe(int roomId);

    void Publish(RoomLiveEventDto liveEvent);
}

public sealed class RoomLiveTelemetrySubscription : IAsyncDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public RoomLiveTelemetrySubscription(ChannelReader<RoomLiveEventDto> reader, Action onDispose)
    {
        Reader = reader;
        _onDispose = onDispose;
    }

    public ChannelReader<RoomLiveEventDto> Reader { get; }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _onDispose();
        return ValueTask.CompletedTask;
    }
}
