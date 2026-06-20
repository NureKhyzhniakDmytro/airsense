using System.Security.Claims;
using System.Text.Json;
using Airsense.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Airsense.API.Controllers;

[ApiController]
[Route("notifications/live")]
[Authorize]
public class NotificationLiveController(
    IUserNotificationHub notificationHub,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    [HttpGet]
    public async Task<IActionResult> Stream()
    {
        if (!int.TryParse(User.FindFirstValue("id"), out var userId))
            return BadRequest(new { message = "You are not registered" });

        var cancellationToken = HttpContext.RequestAborted;
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await using var subscription = notificationHub.Subscribe(userId);

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

                while (subscription.Reader.TryRead(out var notification))
                    await WriteEventAsync("notification", notification, cancellationToken);
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
