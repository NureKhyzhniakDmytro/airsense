using System.Text;
using System.Text.Json;
using Airsense.API.Models.Dto.Ai;
using Airsense.API.Repository;
using Airsense.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("ai")]
[Authorize]
public class AiController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IRoomRepository roomRepository,
    IAiPredictionService aiPredictionService) : ControllerBase
{
    private string PredictionServiceUrl =>
        configuration["Ai:PredictionServiceUrl"]?.TrimEnd('/') ?? "http://ai-prediction-service:8000";

    [HttpGet("health")]
    public Task<IActionResult> Health() => ProxyAsync(HttpMethod.Get, "/health");

    [HttpGet("model/version")]
    public Task<IActionResult> ModelVersion() => ProxyAsync(HttpMethod.Get, "/model/version");

    [HttpPost("predict")]
    public Task<IActionResult> Predict([FromBody] JsonElement request) =>
        ProxyAsync(HttpMethod.Post, "/predict", request);

    [HttpPost("simulate")]
    public Task<IActionResult> Simulate([FromBody] JsonElement request) =>
        ProxyAsync(HttpMethod.Post, "/simulate", request);

    [HttpPost("recommendation")]
    public Task<IActionResult> Recommendation([FromBody] JsonElement request) =>
        ProxyAsync(HttpMethod.Post, "/recommendation", request);

    [HttpGet("room/{roomId:int}")]
    public async Task<IActionResult> RoomInsights(int roomId, CancellationToken cancellationToken)
    {
        var accessError = await ValidateRoomReadAccessAsync(roomId);
        if (accessError is not null)
            return accessError;

        try
        {
            return Ok(await aiPredictionService.GetRoomInsightsAsync(roomId, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "AI prediction service is unavailable"
            });
        }
    }

    [HttpPost("room/{roomId:int}/recommendation")]
    public async Task<IActionResult> CreateRoomRecommendation(int roomId, CancellationToken cancellationToken)
    {
        var accessError = await ValidateRoomManageAccessAsync(roomId);
        if (accessError is not null)
            return accessError;

        try
        {
            return Ok(await aiPredictionService.CreateRoomRecommendationAsync(roomId, cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "AI prediction service is unavailable"
            });
        }
    }

    [HttpPost("room/{roomId:int}/recommendations/{recommendationId:long}/accept")]
    public async Task<IActionResult> AcceptRoomRecommendation(
        int roomId,
        long recommendationId,
        CancellationToken cancellationToken)
    {
        var accessError = await ValidateRoomManageAccessAsync(roomId);
        if (accessError is not null)
            return accessError;

        AiRecommendationAuditDto? accepted;
        try
        {
            accepted = await aiPredictionService.AcceptRecommendationAsync(roomId, recommendationId, cancellationToken);
            if (accepted is null)
                return NotFound(new { message = "AI recommendation not found" });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }

        return Ok(accepted);
    }

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, JsonElement? body = null)
    {
        using var request = new HttpRequestMessage(method, $"{PredictionServiceUrl}{path}");
        if (body.HasValue)
        {
            request.Content = new StringContent(body.Value.GetRawText(), Encoding.UTF8, "application/json");
        }

        var client = httpClientFactory.CreateClient();

        try
        {
            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "AI prediction service is unavailable"
            });
        }
    }

    private Task<IActionResult?> ValidateRoomReadAccessAsync(int roomId) =>
        ValidateRoomAccessAsync(roomId, requireManageAccess: false);

    private Task<IActionResult?> ValidateRoomManageAccessAsync(int roomId) =>
        ValidateRoomAccessAsync(roomId, requireManageAccess: true);

    private async Task<IActionResult?> ValidateRoomAccessAsync(int roomId, bool requireManageAccess)
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out var userId))
            return BadRequest(new { message = "You are not registered" });

        var room = await roomRepository.GetByIdAsync(roomId);
        if (room is null)
            return NotFound(new { message = "Room not found" });

        var hasAccess = requireManageAccess
            ? await roomRepository.IsHasAccessAsync(userId, roomId)
            : await roomRepository.IsMemberAsync(userId, roomId);

        if (!hasAccess)
            return Forbid();

        return null;
    }
}
