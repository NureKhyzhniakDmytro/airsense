namespace Airsense.API.Repository;

public enum ThresholdAlertTransition
{
    None,
    Triggered,
    Resolved
}

public interface IThresholdAlertStateRepository
{
    Task<ThresholdAlertTransition> UpdateAsync(int roomId, int sensorId, string parameter, double value, double criticalValue);
}
