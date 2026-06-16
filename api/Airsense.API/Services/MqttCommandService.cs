using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Airsense.API.Services;

public class MqttCommandService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<MqttServiceBase> logger,
    IOptions<JsonOptions> jsonOptions,
    MqttClientOptions mqttOptions)
    : MqttServiceBase(serviceProvider, configuration, logger, jsonOptions, mqttOptions);
