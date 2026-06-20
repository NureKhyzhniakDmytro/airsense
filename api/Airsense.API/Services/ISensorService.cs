using Airsense.API.Models.Dto.Sensor;

namespace Airsense.API.Services;

public interface ISensorService
{
    public Task ProcessDataAsync(int roomId, int sensorId, string parameter, SensorDataDto data);
}
