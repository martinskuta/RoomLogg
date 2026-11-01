using System.Text.Json.Serialization;
using RoomLogg.Device;

namespace RoomLogg;

internal readonly record struct ChannelStatePayload(
    [property: JsonPropertyName("temperature")]
    double Temperature,
    [property: JsonPropertyName("humidity")]
    double Humidity,
    [property: JsonPropertyName("dew_point")]
    double DewPoint,
    [property: JsonPropertyName("heat_index")]
    double HeatIndex)
{
    public static bool TryCreate(SensorReading reading, out ChannelStatePayload payload)
    {
        if (reading.Temperature is not { } temperature ||
            reading.Humidity is not { } humidity ||
            reading.DewPoint is not { } dewPoint ||
            reading.HeatIndex is not { } heatIndex)
        {
            payload = default;
            return false;
        }

        payload = new ChannelStatePayload(
            Math.Round(temperature.DegreesCelsius, 1),
            Math.Round(humidity.Percent, 0),
            Math.Round(dewPoint.DegreesCelsius, 1),
            Math.Round(heatIndex.DegreesCelsius, 1));
        return true;
    }
}

internal sealed record HomeAssistantDeviceDiscoveryPayload(
    [property: JsonPropertyName("device")] HomeAssistantDeviceInfo Device,
    [property: JsonPropertyName("origin")] HomeAssistantOriginInfo Origin,
    [property: JsonPropertyName("state_topic")]
    string StateTopic,
    [property: JsonPropertyName("components")]
    Dictionary<string, HomeAssistantComponentConfig> Components)
{
    public static HomeAssistantDeviceDiscoveryPayload CreateForChannel(int channel)
    {
        if (channel is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 1 and 8.");

        var prefix = $"roomlogg_channel_{channel}";
        return new HomeAssistantDeviceDiscoveryPayload(
            new HomeAssistantDeviceInfo([prefix], $"RoomLogg Channel {channel}", "Dostmann Electronic", "RoomLogg PRO"),
            new HomeAssistantOriginInfo("RoomLogg", "https://github.com/martinskuta/RoomLogg"),
            $"roomlogg/channel/{channel}/state",
            new Dictionary<string, HomeAssistantComponentConfig>(StringComparer.Ordinal)
            {
                ["temperature"] = new HomeAssistantComponentConfig(
                    "sensor", "Temperature", "temperature", "°C", "measurement",
                    "{{ value_json.temperature }}", $"{prefix}_temperature"),
                ["humidity"] = new HomeAssistantComponentConfig(
                    "sensor", "Humidity", "humidity", "%", "measurement",
                    "{{ value_json.humidity }}", $"{prefix}_humidity"),
                ["dew_point"] = new HomeAssistantComponentConfig(
                    "sensor", "Dew point", "temperature", "°C", "measurement",
                    "{{ value_json.dew_point }}", $"{prefix}_dew_point"),
                ["heat_index"] = new HomeAssistantComponentConfig(
                    "sensor", "Heat index", "temperature", "°C", "measurement",
                    "{{ value_json.heat_index }}", $"{prefix}_heat_index")
            });
    }
}

internal sealed record HomeAssistantDeviceInfo(
    [property: JsonPropertyName("identifiers")]
    string[] Identifiers,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("manufacturer")]
    string Manufacturer,
    [property: JsonPropertyName("model")] string Model);

internal sealed record HomeAssistantOriginInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url);

internal sealed record HomeAssistantComponentConfig(
    [property: JsonPropertyName("platform")]
    string Platform,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("device_class")]
    string DeviceClass,
    [property: JsonPropertyName("unit_of_measurement")]
    string UnitOfMeasurement,
    [property: JsonPropertyName("state_class")]
    string StateClass,
    [property: JsonPropertyName("value_template")]
    string ValueTemplate,
    [property: JsonPropertyName("unique_id")]
    string UniqueId);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ChannelStatePayload))]
[JsonSerializable(typeof(HomeAssistantDeviceDiscoveryPayload))]
internal partial class MqttJsonContext : JsonSerializerContext;