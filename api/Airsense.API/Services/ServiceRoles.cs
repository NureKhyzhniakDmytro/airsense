namespace Airsense.API.Services;

public static class ServiceRoles
{
    public const string Api = "api";
    public const string TelemetryIngestion = "telemetry-ingestion";
    public const string Automation = "automation";
    public const string Notification = "notification";

    public static bool IsApi(string role) => role.Equals(Api, StringComparison.OrdinalIgnoreCase);
    public static bool IsTelemetryIngestion(string role) => role.Equals(TelemetryIngestion, StringComparison.OrdinalIgnoreCase);
    public static bool IsAutomation(string role) => role.Equals(Automation, StringComparison.OrdinalIgnoreCase);
    public static bool IsNotification(string role) => role.Equals(Notification, StringComparison.OrdinalIgnoreCase);
}
