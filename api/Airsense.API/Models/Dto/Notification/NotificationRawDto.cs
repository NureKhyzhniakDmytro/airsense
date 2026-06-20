namespace Airsense.API.Models.Dto.Notification;

public class NotificationRawDto
{
    public int UserId { get; set; }

    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";

    public string DataJson { get; set; } = "{}";

    public long CreatedAt { get; set; }

    public long? ReadAt { get; set; }
}
