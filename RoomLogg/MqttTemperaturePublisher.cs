using System.Globalization;
using MQTTnet;
using MQTTnet.Protocol;
using RoomLogg.Device;

namespace RoomLogg;

internal sealed class MqttTemperaturePublisher : IDisposable
{
    private readonly IMqttClient? _client;
    private readonly string _host;
    private readonly MqttClientOptions? _options;
    private readonly int _port;
    private readonly ChangedValuesTracker _reportedValues = new();

    private MqttTemperaturePublisher(string host, int port, string? username, string? password)
    {
        host = host.Trim();
        _host = host;
        _port = port;

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

        return new MqttTemperaturePublisher(
            host,
            port,
            Environment.GetEnvironmentVariable("MQTT_USERNAME"),
            Environment.GetEnvironmentVariable("MQTT_PASSWORD"));
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

                await PublishIfChangedAsync(reading.Channel, "temperature", reading.Temperature?.DegreesCelsius);
                await PublishIfChangedAsync(reading.Channel, "humidity", reading.Humidity?.Percent);
                await PublishIfChangedAsync(reading.Channel, "dew_point", reading.DewPoint?.DegreesCelsius);
                await PublishIfChangedAsync(reading.Channel, "heat_index", reading.HeatIndex?.DegreesCelsius);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Unable to publish RoomLogg readings to MQTT at {_host}:{_port}: {exception.Message}");
        }
    }

    private async Task PublishIfChangedAsync(int channel, string metric, double? value)
    {
        if (value is not { } actualValue)
            return;

        var topic = $"roomlogg/channel/{channel}/{metric}";
        if (_reportedValues.WasReported(topic, actualValue))
            return;

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(actualValue.ToString("G", CultureInfo.InvariantCulture))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        await _client!.PublishAsync(message);
        _reportedValues.MarkReported(topic, actualValue);
    }
}

internal sealed class ChangedValuesTracker
{
    private readonly Dictionary<string, double> _reportedValues = new(StringComparer.Ordinal);

    public bool WasReported(string topic, double value) =>
        _reportedValues.TryGetValue(topic, out var reportedValue) && reportedValue.Equals(value);

    public void MarkReported(string topic, double value) => _reportedValues[topic] = value;
}