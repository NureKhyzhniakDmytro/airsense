using Airsense.API.Models.Dto.Device;
using Airsense.API.Models.Dto.Sensor;

namespace Airsense.API.Models.Dto.Messaging;

public class RoomLiveEventDto
{
    public string Type { get; set; } = string.Empty;

    public int RoomId { get; set; }

    public int? SensorId { get; set; }

    public string? SensorSerialNumber { get; set; }

    public string? Parameter { get; set; }

    public double? Value { get; set; }

    public long? SentAt { get; set; }

    public int? DeviceId { get; set; }

    public string? DeviceSerialNumber { get; set; }

    public double? FanSpeed { get; set; }

    public long? ActiveAt { get; set; }

    public string? Source { get; set; }
}

public class RoomLiveSnapshotDto
{
    public int RoomId { get; set; }

    public ICollection<SensorDto> Sensors { get; set; } = Array.Empty<SensorDto>();

    public ICollection<DeviceDto> Devices { get; set; } = Array.Empty<DeviceDto>();

    public long GeneratedAt { get; set; }
}
