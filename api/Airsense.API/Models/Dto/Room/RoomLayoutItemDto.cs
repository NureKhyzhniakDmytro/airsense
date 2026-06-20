using System.ComponentModel.DataAnnotations;

namespace Airsense.API.Models.Dto.Room;

public class RoomLayoutItemDto
{
    [Required]
    [Length(1, 80)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [Length(1, 64)]
    public string Type { get; set; } = string.Empty;

    [Length(0, 80)]
    public string? Label { get; set; }

    [Range(1, int.MaxValue)]
    public int? SensorId { get; set; }

    [Range(1, int.MaxValue)]
    public int? DeviceId { get; set; }

    [Length(0, 120)]
    public string? SerialNumber { get; set; }

    [RegularExpression("^(supply|exhaust)$")]
    public string? AirflowRole { get; set; }

    [Range(0, 1000)]
    public double? HeatLoadKw { get; set; }

    [RegularExpression("^(low|medium|high)$")]
    public string? ThermalLoad { get; set; }

    [Range(-1000, 1000)]
    public double X { get; set; }

    [Range(-1000, 1000)]
    public double Y { get; set; }

    [Range(0.1, 1000)]
    public double Width { get; set; } = 0.8;

    [Range(0.1, 1000)]
    public double Height { get; set; } = 0.8;

    [Range(-360, 360)]
    public double Rotation { get; set; }
}
