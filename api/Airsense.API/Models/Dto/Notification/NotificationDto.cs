namespace Airsense.API.Models.Dto.Notification;

public class NotificationDto
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";

    public Dictionary<string, string>? Data { get; set; }

    public long CreatedAt { get; set; }

    public long? ReadAt { get; set; }

    public bool IsRead => ReadAt.HasValue;
}
