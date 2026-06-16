namespace Airsense.API.Models.Dto.Messaging;

public class NotificationEventDto
{
    public ICollection<string> DeviceTokens { get; set; } = new List<string>();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string>? Data { get; set; }
}
