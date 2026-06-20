using Airsense.API.Models.Dto.Room;
using Airsense.API.Services;
using Xunit;

namespace Airsense.API.Tests;

public class RoomLayoutValidatorTests
{
    [Fact]
    public void Validate_AllowsBoundSensorAndVentInsideRoom()
    {
        var layout = CreateRectangleLayout(
            new RoomLayoutItemDto
            {
                Id = "sensor-1",
                Type = "sensor",
                Label = "Sensor #1",
                SensorId = 1,
                X = 1,
                Y = 1,
                Width = 0.5,
                Height = 0.5
            },
            new RoomLayoutItemDto
            {
                Id = "vent-1",
                Type = "vent",
                Label = "Vent #1",
                DeviceId = 1,
                X = 2,
                Y = 1,
                Width = 0.6,
                Height = 0.6
            });

        var errors = RoomLayoutValidator.Validate(layout);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RequiresBoundSensorReference()
    {
        var layout = CreateRectangleLayout(
            new RoomLayoutItemDto
            {
                Id = "sensor-1",
                Type = "sensor",
                Label = "Sensor #1",
                X = 1,
                Y = 1,
                Width = 0.5,
                Height = 0.5
            });

        var errors = RoomLayoutValidator.Validate(layout);

        Assert.Contains(errors, error => error.Contains("must reference sensor_id", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsRoomBoundItemOutsideContour()
    {
        var layout = CreateRectangleLayout(
            new RoomLayoutItemDto
            {
                Id = "vent-1",
                Type = "vent",
                Label = "Vent #1",
                DeviceId = 1,
                X = 5.8,
                Y = 3.8,
                Width = 0.6,
                Height = 0.6
            });

        var errors = RoomLayoutValidator.Validate(layout);

        Assert.Contains(errors, error => error.Contains("must be fully inside the room contour", StringComparison.Ordinal));
    }

    private static RoomLayoutDto CreateRectangleLayout(params RoomLayoutItemDto[] items) => new()
    {
        Width = 6,
        Height = 4,
        Unit = "m",
        Geometry = new RoomLayoutGeometryDto
        {
            Type = "rectangle",
            Points = new List<RoomLayoutPointDto>
            {
                new() { X = 0, Y = 0 },
                new() { X = 6, Y = 0 },
                new() { X = 6, Y = 4 },
                new() { X = 0, Y = 4 }
            }
        },
        Items = items.ToList()
    };
}
