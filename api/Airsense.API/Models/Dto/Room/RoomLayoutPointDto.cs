using System.ComponentModel.DataAnnotations;

namespace Airsense.API.Models.Dto.Room;

public class RoomLayoutPointDto
{
    [Range(0, 1000)]
    public double X { get; set; }

    [Range(0, 1000)]
    public double Y { get; set; }
}
