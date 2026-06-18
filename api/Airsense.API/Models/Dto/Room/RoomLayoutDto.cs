using System.ComponentModel.DataAnnotations;

namespace Airsense.API.Models.Dto.Room;

public class RoomLayoutDto
{
    [Range(1, 1000)]
    public double Width { get; set; } = 6;

    [Range(1, 1000)]
    public double Height { get; set; } = 4;

    [Length(1, 12)]
    public string Unit { get; set; } = "m";

    public RoomLayoutGeometryDto Geometry { get; set; } = new();

    public ICollection<RoomLayoutItemDto> Items { get; set; } = new List<RoomLayoutItemDto>();
}
