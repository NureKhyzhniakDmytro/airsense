using Airsense.API.Models.Dto.Ai;

namespace Airsense.API.Services;

public interface IAiPredictionService
{
    Task<RoomAiInsightsDto> GetRoomInsightsAsync(int roomId, CancellationToken cancellationToken = default);

    Task<AiRecommendationAuditDto> CreateRoomRecommendationAsync(int roomId, CancellationToken cancellationToken = default);

    Task<AiRecommendationAuditDto?> AcceptRecommendationAsync(
        int roomId,
        long recommendationId,
        CancellationToken cancellationToken = default);

    Task<AiControlSettingsDto> GetControlSettingsAsync(int roomId);

    Task<AiControlSettingsDto> UpdateControlSettingsAsync(
        int roomId,
        AiControlSettingsUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<AiAutomationRecommendationDto?> ConsumeAcceptedRecommendationAsync(int roomId);

    Task<AiAutomationRecommendationDto?> GetAutonomousControlRecommendationAsync(
        int roomId,
        CancellationToken cancellationToken = default);
}
