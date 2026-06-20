using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Airsense.API.Services;

public abstract class MqttServiceBase(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<MqttServiceBase> logger,
    IOptions<JsonOptions> jsonOptions,
    MqttClientOptions mqttOptions) : BackgroundService, IMqttService
{
    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly MqttClientFactory _mqttFactory = new ();
    private IMqttClient? _mqttClient;
    private readonly ConcurrentDictionary<string, Func<MqttApplicationMessageReceivedEventArgs, Task>> _callbacks = new();

    protected void RegisterCallback(string topicFilter, Func<MqttApplicationMessageReceivedEventArgs, Task> callback) => _callbacks[topicFilter] = callback;

    private async Task HandleReceivedMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;

        foreach (var kvp in _callbacks)
        {
            if (!IsTopicMatch(kvp.Key, topic))
                continue;

            try
            {
                await kvp.Value(e);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process MQTT message from topic {Topic} with filter {Filter}", topic, kvp.Key);
            }
        }
    }

    private bool IsTopicMatch(string filter, string topic)
    {
        if (filter.Equals(topic, StringComparison.OrdinalIgnoreCase))
            return true;

        var regexPattern = "^" + Regex.Escape(filter)
            .Replace("\\+", "[^/]+")
            .Replace("\\#", ".*") + "$";

        return Regex.IsMatch(topic, regexPattern, RegexOptions.IgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mqttClient = _mqttFactory.CreateMqttClient();

        _mqttClient.ConnectedAsync += OnConnected;
        _mqttClient.DisconnectedAsync += OnDisconnected;
        _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedMessageAsync;

        mqttOptions.Credentials =
            new MqttClientCredentials("api", Encoding.UTF8.GetBytes(AuthMqttService.GetApiCredentials(configuration)));

        var reconnectDelay = InitialReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_mqttClient.IsConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            try
            {
                logger.LogInformation("Connecting MQTT client to {Server}", mqttOptions.ChannelOptions);
                await _mqttClient.ConnectAsync(mqttOptions, stoppingToken);
                reconnectDelay = InitialReconnectDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT connection failed. Retrying in {DelaySeconds} seconds", reconnectDelay.TotalSeconds);
                await Task.Delay(reconnectDelay, stoppingToken);
                reconnectDelay = TimeSpan.FromSeconds(Math.Min(reconnectDelay.TotalSeconds * 2, MaxReconnectDelay.TotalSeconds));
            }
        }
    }

    private async Task OnConnected(MqttClientConnectedEventArgs e)
    {
        if (_mqttClient is null)
            return;

        var topicFilters = _callbacks
            .Select(t => new MqttTopicFilterBuilder()
                .WithTopic(t.Key)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build()
            )
            .ToList();

        if (topicFilters.Count == 0)
        {
            logger.LogInformation("MQTT client connected without subscriptions");
            return;
        }

        await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptions
        {
            TopicFilters = topicFilters
        });

        logger.LogInformation("MQTT client connected and subscribed to {SubscriptionCount} topic filters", topicFilters.Count);
    }
    
    private Task OnDisconnected(MqttClientDisconnectedEventArgs e)
    {
        logger.LogWarning("MQTT client disconnected: {Reason}", e.Reason);
        return Task.CompletedTask;
    } 

    protected T? Deserialize<T>(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, jsonOptions.Value.JsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize MQTT payload as {PayloadType}", typeof(T).Name);
            return default;
        }
    }

    protected IServiceProvider GetServiceProvider() => serviceProvider;

    public Task PublishAsync(string topic, object payload) => 
        PublishAsync(topic, JsonSerializer.Serialize(payload, jsonOptions.Value.JsonSerializerOptions));

    public async Task PublishAsync(string topic, string payload)
    {
        if (_mqttClient is null || !_mqttClient.IsConnected)
        {
            logger.LogWarning("Skipping MQTT publish to {Topic} because the client is not connected", topic);
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _mqttClient.PublishAsync(message);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient is not null && _mqttClient.IsConnected)
        {
            var disconnectOptions = new MqttClientDisconnectOptionsBuilder()
                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                .Build();
            await _mqttClient.DisconnectAsync(disconnectOptions, cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
