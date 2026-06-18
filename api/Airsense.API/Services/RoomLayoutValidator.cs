using Airsense.API.Models.Dto.Room;

namespace Airsense.API.Services;

public static class RoomLayoutValidator
{
    private const double Epsilon = 0.000001;

    public static IReadOnlyCollection<string> Validate(RoomLayoutDto layout)
    {
        var errors = new List<string>();
        if (layout.Geometry?.Points == null)
        {
            errors.Add("Room geometry must contain at least three points.");
            return errors;
        }

        var polygon = GetPolygon(layout);

        if (polygon.Count < 3)
        {
            errors.Add("Room geometry must contain at least three points.");
            return errors;
        }

        foreach (var item in (layout.Items ?? Enumerable.Empty<RoomLayoutItemDto>()).Where(item => IsRoomBoundItem(item.Type)))
        {
            if (IsItemInsideRoom(item, layout, polygon))
                continue;

            var itemName = string.IsNullOrWhiteSpace(item.Label) ? item.Id : item.Label;
            errors.Add($"{GetRoomBoundItemTypeName(item.Type)} \"{itemName}\" must be fully inside the room contour.");
        }

        return errors;
    }

    private static List<Point> GetPolygon(RoomLayoutDto layout)
    {
        var points = layout.Geometry.Points
            .Select(point => new Point(point.X, point.Y))
            .ToList();

        if (points.Count > 1 && AreSamePoint(points[0], points[^1]))
            points.RemoveAt(points.Count - 1);

        return points;
    }

    private static bool IsRoomBoundItem(string type)
    {
        return string.Equals(type, "sensor", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "vent", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRoomBoundItemTypeName(string type)
    {
        return string.Equals(type, "vent", StringComparison.OrdinalIgnoreCase) ? "Ventilation" : "Sensor";
    }

    private static bool IsItemInsideRoom(RoomLayoutItemDto item, RoomLayoutDto layout, IReadOnlyList<Point> polygon)
    {
        return GetItemProbePoints(item).All(point =>
            point.X >= -Epsilon
            && point.X <= layout.Width + Epsilon
            && point.Y >= -Epsilon
            && point.Y <= layout.Height + Epsilon
            && IsPointInsidePolygon(point, polygon)
        );
    }

    private static IEnumerable<Point> GetItemProbePoints(RoomLayoutItemDto item)
    {
        var left = item.X;
        var right = item.X + item.Width;
        var top = item.Y;
        var bottom = item.Y + item.Height;
        var center = new Point((left + right) / 2, (top + bottom) / 2);
        var points = new[]
        {
            new Point(left, top),
            new Point(center.X, top),
            new Point(right, top),
            new Point(right, center.Y),
            new Point(right, bottom),
            new Point(center.X, bottom),
            new Point(left, bottom),
            new Point(left, center.Y),
            center
        };

        var rotation = item.Rotation * Math.PI / 180;
        if (Math.Abs(rotation) < Epsilon)
            return points;

        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);

        return points.Select(point =>
        {
            var x = point.X - center.X;
            var y = point.Y - center.Y;
            return new Point(
                center.X + x * cos - y * sin,
                center.Y + x * sin + y * cos
            );
        });
    }

    private static bool IsPointInsidePolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;

        for (int index = 0, previousIndex = polygon.Count - 1; index < polygon.Count; previousIndex = index++)
        {
            var current = polygon[index];
            var previous = polygon[previousIndex];

            if (IsPointOnSegment(point, previous, current))
                return true;

            var intersects = current.Y > point.Y != previous.Y > point.Y
                             && point.X <= (previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y) + current.X + Epsilon;

            if (intersects)
                inside = !inside;
        }

        return inside;
    }

    private static bool IsPointOnSegment(Point point, Point start, Point end)
    {
        var cross = (point.Y - start.Y) * (end.X - start.X) - (point.X - start.X) * (end.Y - start.Y);
        if (Math.Abs(cross) > Epsilon)
            return false;

        var dot = (point.X - start.X) * (end.X - start.X) + (point.Y - start.Y) * (end.Y - start.Y);
        if (dot < -Epsilon)
            return false;

        var squaredLength = Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2);
        return dot <= squaredLength + Epsilon;
    }

    private static bool AreSamePoint(Point first, Point second)
    {
        return Math.Abs(first.X - second.X) < Epsilon && Math.Abs(first.Y - second.Y) < Epsilon;
    }

    private readonly record struct Point(double X, double Y);
}
