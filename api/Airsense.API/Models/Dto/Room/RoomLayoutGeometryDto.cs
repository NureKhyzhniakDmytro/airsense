using System.ComponentModel.DataAnnotations;

namespace Airsense.API.Models.Dto.Room;

public class RoomLayoutGeometryDto
{
    [Required]
    [Length(1, 32)]
    public string Type { get; set; } = "rectangle";

    public ICollection<RoomLayoutPointDto> Points { get; set; } = new List<RoomLayoutPointDto>();
}
