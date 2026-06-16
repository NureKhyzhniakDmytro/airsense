namespace Airsense.API.Services;

public class NoOpNotificationService : INotificationService
{
    public Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null) => Task.FromResult(true);
    public Task SendNotificationAsync(ICollection<string> deviceTokens, string title, string body, Dictionary<string, string>? data = null) => Task.CompletedTask;
}
