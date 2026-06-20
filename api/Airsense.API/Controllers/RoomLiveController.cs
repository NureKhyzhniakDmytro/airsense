using System.Security.Claims;
using System.Text.Json;
using Airsense.API.Models.Dto.Messaging;
using Airsense.API.Repository;
using Airsense.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Airsense.API.Controllers;

[ApiController]
[Route("room/{roomId:int}/live")]
[Authorize]
public class RoomLiveController(
    IRoomRepository roomRepository,
    ISensorRepository sensorRepository,
    IDeviceRepository deviceRepository,
    IRoomLiveTelemetryHub liveHub,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    [HttpGet]
    public async Task<IActionResult> Stream(int roomId)
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        var room = await roomRepository.GetByIdAsync(roomId);
        if (room is null)
            return NotFound(new { message = "Room not found" });

        if (!await roomRepository.IsMemberAsync(userId, roomId))
            return Forbid();

        var cancellationToken = HttpContext.RequestAborted;
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await using var subscription = liveHub.Subscribe(roomId);
        await WriteEventAsync("snapshot", new RoomLiveSnapshotDto
        {
            RoomId = roomId,
            Sensors = await sensorRepository.GetAsync(roomId, 1000, 0),
            Devices = await deviceRepository.GetAsync(roomId, 1000, 0),
            GeneratedAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()
        }, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                waitCancellation.CancelAfter(HeartbeatInterval);

                bool canRead;
                try
                {
                    canRead = await subscription.Reader.WaitToReadAsync(waitCancellation.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await WriteHeartbeatAsync(cancellationToken);
                    continue;
                }

                if (!canRead)
                    break;

                while (subscription.Reader.TryRead(out var liveEvent))
                    await WriteEventAsync(liveEvent.Type, liveEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected.
        }

        return new EmptyResult();
    }

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(payload, jsonOptions.Value.JsonSerializerOptions);
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteHeartbeatAsync(CancellationToken cancellationToken)
    {
        await Response.WriteAsync($": heartbeat {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
