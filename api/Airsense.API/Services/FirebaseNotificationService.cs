using FirebaseAdmin.Messaging;

namespace Airsense.API.Services;

public class FirebaseNotificationService(ILogger<FirebaseNotificationService> logger) : INotificationService
{
    public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
    {
        var messaging = FirebaseMessaging.DefaultInstance; 
        try
        {
            var result = await messaging.SendAsync(new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data?.ToDictionary()
            });
            var sent = !string.IsNullOrEmpty(result);
            logger.LogInformation("Firebase notification send result: sent={Sent}", sent);
            return sent;
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "Firebase notification send failed: code={MessagingErrorCode}", ex.MessagingErrorCode);
            return false;
        }
    }
    
    public async Task SendNotificationAsync(ICollection<string> deviceTokens, string title, string body, Dictionary<string, string>? data = null)
    {
        if (deviceTokens.Count == 0)
            return;

        var messaging = FirebaseMessaging.DefaultInstance;
        try
        {
            var response = await messaging.SendEachForMulticastAsync(new MulticastMessage
            {
                Tokens = deviceTokens.Distinct().ToList(),
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data?.ToDictionary()
            });

            logger.LogInformation(
                "Firebase multicast result: success={SuccessCount}, failure={FailureCount}",
                response.SuccessCount,
                response.FailureCount);

            foreach (var failure in response.Responses.Where(result => !result.IsSuccess))
            {
                var exception = failure.Exception;
                logger.LogWarning(
                    exception,
                    "Firebase multicast item failed: code={MessagingErrorCode}",
                    exception?.MessagingErrorCode);
            }
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "Firebase multicast send failed: code={MessagingErrorCode}", ex.MessagingErrorCode);
        }
    }
    
}
