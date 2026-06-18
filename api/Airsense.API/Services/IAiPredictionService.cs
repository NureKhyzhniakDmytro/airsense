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

    Task<AiAutomationRecommendationDto?> ConsumeAcceptedRecommendationAsync(int roomId);
}
