using Airsense.API.Models.Dto.Sensor;

namespace Airsense.API.Models.Dto.Messaging;

public class TelemetryEventDto
{
    public int RoomId { get; set; }

    public int SensorId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public SensorDataDto Data { get; set; } = new();
}
