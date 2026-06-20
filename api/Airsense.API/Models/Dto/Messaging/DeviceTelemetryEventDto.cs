namespace Airsense.API.Models.Dto.Messaging;

public class DeviceTelemetryEventDto
{
    public int RoomId { get; set; }

    public int? DeviceId { get; set; }

    public string? SerialNumber { get; set; }

    public double FanSpeed { get; set; }

    public long ActiveAt { get; set; }

    public string? Source { get; set; }
}
