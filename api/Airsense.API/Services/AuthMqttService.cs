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
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.Username))
            return "ignore";

        var targetSecret = string.Empty;
        if (request.ClientId.StartsWith("s-", StringComparison.Ordinal)) targetSecret = await GetSensorSecretAsync(request.Username);
        else if (request.ClientId.StartsWith("d-", StringComparison.Ordinal)) targetSecret = await GetDeviceSecretAsync(request.Username);
        else if (request.ClientId.StartsWith("api", StringComparison.Ordinal)) targetSecret = _apiSecret;
        if (string.IsNullOrEmpty(targetSecret)) return "ignore";
        return targetSecret.Equals(ComputeMd5Hash(request.Password + request.Username)) ? "allow" : "deny";
    }

    public async Task<string> AuthorizeAsync(MqttAclRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.Topic))
            return "ignore";

        var result = "ignore";
        if (request.ClientId.StartsWith("s-", StringComparison.Ordinal)) result = await AuthorizeSensorAsync(request);
        else if (request.ClientId.StartsWith("d-", StringComparison.Ordinal)) result = await AuthorizeDeviceAsync(request);
        else if (request.ClientId.StartsWith("api", StringComparison.Ordinal)) result = await AuthorizeApiServerAsync(request);
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
        if (string.Equals(request.Action, "publish", StringComparison.OrdinalIgnoreCase)) return "deny";
        var topic = request.Topic.Split('/');
        if (topic.Length < 2) return "deny";
        if (string.Equals(topic[0], "room", StringComparison.Ordinal) &&
            string.Equals(topic[1], device.RoomId?.ToString(), StringComparison.Ordinal))
            return "allow";
        if (string.Equals(topic[0], "device", StringComparison.Ordinal) &&
            string.Equals(topic[1], device.Id.ToString(), StringComparison.Ordinal))
            return "allow";
        return "deny";
    }

    private async Task<string> AuthorizeSensorAsync(MqttAclRequestDto request)
    {
        var sensor = await _sensorRepository.GetBySerialNumberAsync(request.Username);
        if (sensor is null) return "ignore";
        if (string.Equals(request.Action, "subscribe", StringComparison.OrdinalIgnoreCase)) return "deny";
        var topic = request.Topic.Split('/');
        if (topic.Length < 2) return "deny";
        if (string.Equals(topic[0], "sensor", StringComparison.Ordinal))
        {
            var types = await _sensorRepository.GetTypesAsync(sensor.Id);
            if (types.Any(t => string.Equals(topic[1], t, StringComparison.Ordinal))) return "allow";
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
