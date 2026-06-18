namespace Airsense.API.Models.Dto.Ai;

public sealed class AiTelemetrySampleDto
{
    public int RoomId { get; init; }
    public DateTime? Timestamp { get; init; }
    public double Co2 { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public double VentilationPower { get; init; }
    public int Occupancy { get; init; }
}

public sealed class AiPredictionPointDto
{
    public int HorizonMinutes { get; init; }
    public double Co2 { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
}

public sealed class AiPredictRequestDto
{
    public AiTelemetrySampleDto Sample { get; init; } = new();
    public List<int> HorizonsMinutes { get; init; } = [10, 20, 30];
}

public sealed class AiPredictResponseDto
{
    public string ModelVersion { get; init; } = "";
    public string Mode { get; init; } = "";
    public List<AiPredictionPointDto> Predictions { get; init; } = [];
}

public sealed class AiVentilationScenarioDto
{
    public string Label { get; init; } = "";
    public double VentilationPower { get; init; }
}

public sealed class AiSimulateRequestDto
{
    public AiTelemetrySampleDto Sample { get; init; } = new();
    public List<AiVentilationScenarioDto> Scenarios { get; init; } = [];
    public List<int> HorizonsMinutes { get; init; } = [10, 20, 30];
}

public sealed class AiScenarioSimulationDto
{
    public string Label { get; init; } = "";
    public double VentilationPower { get; init; }
    public List<AiPredictionPointDto> Predictions { get; init; } = [];
}

public sealed class AiSimulateResponseDto
{
    public string ModelVersion { get; init; } = "";
    public string Mode { get; init; } = "";
    public List<AiScenarioSimulationDto> Scenarios { get; init; } = [];
}

public sealed class AiRecommendationRequestDto
{
    public AiTelemetrySampleDto Sample { get; init; } = new();
    public double TargetCo2 { get; init; } = 900;
    public double MaxVentilationPower { get; init; } = 100;
    public int HorizonMinutes { get; init; } = 20;
}

public sealed class AiRecommendationResponseDto
{
    public string ModelVersion { get; init; } = "";
    public string Mode { get; init; } = "";
    public double SuggestedVentilationPower { get; init; }
    public string Reason { get; init; } = "";
    public AiPredictionPointDto Predicted { get; init; } = new();
    public string MqttCommandTopic { get; init; } = "";
    public bool SendsCommand { get; init; }
}

public sealed class AiRecommendationPayloadDto
{
    public string ModelVersion { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Reason { get; init; } = "";
    public AiTelemetrySampleDto Sample { get; init; } = new();
    public AiPredictionPointDto Predicted { get; init; } = new();
    public double TargetCo2 { get; init; }
    public double MaxVentilationPower { get; init; }
    public int HorizonMinutes { get; init; }
}

public sealed class AiRecommendationAuditDto
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public double? RequestedPower { get; init; }
    public string Status { get; init; } = "";
    public string ModelVersion { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Reason { get; init; } = "";
    public AiPredictionPointDto? Predicted { get; init; }
    public AiTelemetrySampleDto? Sample { get; init; }
}

public sealed class RoomAiInsightsDto
{
    public bool HasSample { get; init; }
    public string? Message { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public double? TelemetryAgeSeconds { get; init; }
    public AiTelemetrySampleDto? Sample { get; init; }
    public AiPredictResponseDto? Prediction { get; init; }
    public AiSimulateResponseDto? Simulation { get; init; }
    public List<AiRecommendationAuditDto> RecentRecommendations { get; init; } = [];
}

public sealed class AiAutomationRecommendationDto
{
    public long Id { get; init; }
    public double RequestedPower { get; init; }
}
