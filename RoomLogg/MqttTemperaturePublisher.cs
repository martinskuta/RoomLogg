using System.Globalization;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;
using RoomLogg.Device;

namespace RoomLogg;

internal sealed class MqttTemperaturePublisher : IDisposable
{
    private readonly IMqttClient? _client;
    private readonly HashSet<int> _discoveredChannels = [];
    private readonly bool _homeAssistantDiscovery;
    private readonly string _host;
    private readonly MqttClientOptions? _options;
    private readonly int _port;
    private readonly ChangedValuesTracker _reportedValues = new();

    private MqttTemperaturePublisher(
        string host,
        int port,
        string? username,
        string? password,
        bool homeAssistantDiscovery)
    {
        host = host.Trim();
        _host = host;
        _port = port;
        _homeAssistantDiscovery = homeAssistantDiscovery;

        if (string.IsNullOrWhiteSpace(host))
        {
            Console.WriteLine("MQTT_HOST is not configured; MQTT reporting is disabled.");
        }
        else
        {
            _client = new MqttClientFactory().CreateMqttClient();
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId($"RoomLogg-{Guid.NewGuid():N}");

            if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
                optionsBuilder.WithCredentials(username ?? string.Empty, password ?? string.Empty);

            _options = optionsBuilder.Build();
        }
    }

    public void Dispose() => _client?.Dispose();

    public static MqttTemperaturePublisher FromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("MQTT_HOST") ?? string.Empty;
        var portText = Environment.GetEnvironmentVariable("MQTT_PORT");
        var port = 1883;

        if (!string.IsNullOrWhiteSpace(portText) &&
            (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
             port is < 1 or > 65535))
        {
            throw new InvalidOperationException("MQTT_PORT must be an integer between 1 and 65535.");
        }

        var homeAssistantText = Environment.GetEnvironmentVariable("HOME_ASSISTANT");
        var homeAssistantDiscovery = false;
        if (!string.IsNullOrWhiteSpace(homeAssistantText) &&
            !bool.TryParse(homeAssistantText, out homeAssistantDiscovery))
            throw new InvalidOperationException("HOME_ASSISTANT must be 'true' or 'false'.");

        return new MqttTemperaturePublisher(
            host,
            port,
            Environment.GetEnvironmentVariable("MQTT_USERNAME"),
            Environment.GetEnvironmentVariable("MQTT_PASSWORD"),
            homeAssistantDiscovery);
    }

    public async Task PublishChangesAsync(IEnumerable<SensorReading> readings)
    {
        if (_client is null || _options is null)
            return;

        try
        {
            if (!_client.IsConnected)
                await _client.ConnectAsync(_options);

            foreach (var reading in readings)
            {
                if (!reading.IsConnected)
                    continue;

                if (!ChannelStatePayload.TryCreate(reading, out var payload))
                    continue;

                if (_homeAssistantDiscovery)
                    await PublishDiscoveryIfNeededAsync(reading.Channel);

                await PublishStateIfChangedAsync(reading.Channel, payload);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Unable to publish RoomLogg readings to MQTT at {_host}:{_port}: {exception.Message}");
        }
    }

    private async Task PublishDiscoveryIfNeededAsync(int channel)
    {
        if (_discoveredChannels.Contains(channel))
            return;

        var discovery = HomeAssistantDeviceDiscoveryPayload.CreateForChannel(channel);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"homeassistant/device/roomlogg_channel_{channel}/config")
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(
                discovery,
                MqttJsonContext.Default.HomeAssistantDeviceDiscoveryPayload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        await _client!.PublishAsync(message);
        _discoveredChannels.Add(channel);
    }

    private async Task PublishStateIfChangedAsync(int channel, ChannelStatePayload payload)
    {
        if (_reportedValues.WasReported(channel, payload))
            return;

        var topic = $"roomlogg/channel/{channel}/state";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload, MqttJsonContext.Default.ChannelStatePayload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        await _client!.PublishAsync(message);
        _reportedValues.MarkReported(channel, payload);
    }
}

internal sealed class ChangedValuesTracker
{
    private readonly Dictionary<int, ChannelStatePayload> _reportedValues = [];

    public bool WasReported(int channel, ChannelStatePayload payload) =>
        _reportedValues.TryGetValue(channel, out var reportedPayload) && reportedPayload.Equals(payload);

    public void MarkReported(int channel, ChannelStatePayload payload) => _reportedValues[channel] = payload;
}