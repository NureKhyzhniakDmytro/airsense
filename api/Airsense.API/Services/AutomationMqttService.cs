using System.Text;
using Airsense.API.Models.Dto.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Airsense.API.Services;

public class AutomationMqttService : MqttServiceBase
{
    public AutomationMqttService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MqttServiceBase> logger, IOptions<JsonOptions> jsonOptions, MqttClientOptions mqttOptions)
        : base(serviceProvider, configuration, logger, jsonOptions, mqttOptions)
    {
        RegisterCallback("airsense/telemetry", OnTelemetryAcceptedAsync);
    }

    private async Task OnTelemetryAcceptedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Deserialize<TelemetryEventDto>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        if (payload is null) return;
        using var scope = GetServiceProvider().CreateScope();
        var sensorService = scope.ServiceProvider.GetRequiredService<ISensorService>();
        await sensorService.ProcessDataAsync(payload.RoomId, payload.Parameter, payload.Data);
    }
}
