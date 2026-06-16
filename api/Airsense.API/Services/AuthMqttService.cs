using System.Security.Cryptography;
using System.Text;
using Airsense.API.Models.Dto.Auth;
using Airsense.API.Repository;
using Microsoft.Extensions.Configuration;

namespace Airsense.API.Services;

public class AuthMqttService : IAuthMqttService
{
    private static string _apiSecret = string.Empty;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISensorRepository _sensorRepository;

    public AuthMqttService(IDeviceRepository deviceRepository, ISensorRepository sensorRepository, IConfiguration configuration)
    {
        _deviceRepository = deviceRepository;
        _sensorRepository = sensorRepository;
        var configuredPassword = configuration["Mqtt:ApiPassword"];
        if (!string.IsNullOrWhiteSpace(configuredPassword))
            _apiSecret = ComputeMd5Hash(configuredPassword + "api");
    }

    public async Task<string> AuthenticateAsync(MqttAuthRequestDto request)
    {
        var targetSecret = string.Empty;
        if (request.ClientId.StartsWith("s-")) targetSecret = await GetSensorSecretAsync(request.Username);
        else if (request.ClientId.StartsWith("d-")) targetSecret = await GetDeviceSecretAsync(request.Username);
        else if (request.ClientId.StartsWith("api")) targetSecret = _apiSecret;
        if (string.IsNullOrEmpty(targetSecret)) return "ignore";
        return targetSecret.Equals(ComputeMd5Hash(request.Password + request.Username)) ? "allow" : "deny";
    }

    public async Task<string> AuthorizeAsync(MqttAclRequestDto request)
    {
        var result = "ignore";
        if (request.ClientId.StartsWith("s-")) result = await AuthorizeSensorAsync(request);
        else if (request.ClientId.StartsWith("d-")) result = await AuthorizeDeviceAsync(request);
        else if (request.ClientId.StartsWith("api")) result = await AuthorizeApiServerAsync(request);
        return result;
    }

    internal static string GetApiCredentials(IConfiguration configuration)
    {
        var configuredPassword = configuration["Mqtt:ApiPassword"];
        if (!string.IsNullOrWhiteSpace(configuredPassword))
        {
            _apiSecret = ComputeMd5Hash(configuredPassword + "api");
            return configuredPassword;
        }
        var password = Guid.NewGuid().ToString();
        _apiSecret = ComputeMd5Hash(password + "api");
        return password;
    }

    private async Task<string> GetDeviceSecretAsync(string serialNumber)
    {
        var device = await _deviceRepository.GetBySerialNumberAsync(serialNumber);
        return device?.Secret ?? string.Empty;
    }

    private async Task<string> GetSensorSecretAsync(string serialNumber)
    {
        var sensor = await _sensorRepository.GetBySerialNumberAsync(serialNumber);
        return sensor?.Secret ?? string.Empty;
    }

    private async Task<string> AuthorizeDeviceAsync(MqttAclRequestDto request)
    {
        var device = await _deviceRepository.GetBySerialNumberAsync(request.Username);
        if (device is null) return "ignore";
        if (request.Action.Equals("publish")) return "deny";
        var topic = request.Topic.Split("/");
        if (topic[0].Equals("room") && topic[1].Equals(device.RoomId?.ToString())) return "allow";
        if (topic[0].Equals("device") && topic[1].Equals(device.Id.ToString())) return "allow";
        return "deny";
    }

    private async Task<string> AuthorizeSensorAsync(MqttAclRequestDto request)
    {
        var sensor = await _sensorRepository.GetBySerialNumberAsync(request.Username);
        if (sensor is null) return "ignore";
        if (request.Action.Equals("subscribe")) return "deny";
        var topic = request.Topic.Split("/");
        if (topic[0].Equals("sensor"))
        {
            var types = await _sensorRepository.GetTypesAsync(sensor.Id);
            if (types.Any(t => topic[1].Equals(t))) return "allow";
        }
        return "deny";
    }

    private Task<string> AuthorizeApiServerAsync(MqttAclRequestDto request) => Task.FromResult("allow");

    private static string ComputeMd5Hash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        var sb = new StringBuilder();
        foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
