using System.Globalization;
using RoomLogg;
using RoomLogg.Device;

using var roomLoggManager = new RoomLoggManager();
using var mqttPublisher = MqttTemperaturePublisher.FromEnvironment();

roomLoggManager.Connected += (_, _) => Console.WriteLine("Connected to RoomLogg.");
roomLoggManager.Disconnected += (_, _) => Console.WriteLine("RoomLogg disconnected; reconnecting will be retried.");

while (true)
{
    if (!roomLoggManager.IsConnected && !roomLoggManager.TryConnect())
    {
        Console.WriteLine("RoomLogg was not found. Retrying in one minute.");
        Thread.Sleep(TimeSpan.FromMinutes(1));
        continue;
    }

    var readings = roomLoggManager.ReadTemperatures();
    foreach (var reading in readings)
    {
        if (!reading.IsConnected)
        {
            Console.WriteLine($"Channel {reading.Channel}: disconnected");
            continue;
        }

        var temperature = reading.Temperature?.DegreesCelsius.ToString("F1", CultureInfo.InvariantCulture) ?? "N/A";
        var humidity = reading.Humidity?.Percent.ToString("F0", CultureInfo.InvariantCulture) ?? "N/A";
        var dewPoint = reading.DewPoint?.DegreesCelsius.ToString("F1", CultureInfo.InvariantCulture) ?? "N/A";
        var heatIndex = reading.HeatIndex?.DegreesCelsius.ToString("F1", CultureInfo.InvariantCulture) ?? "N/A";

        Console.WriteLine(
            $"Channel {reading.Channel}: {temperature} °C, {humidity}% RH, {dewPoint} °C dew point, {heatIndex} °C heat index");
    }

    await mqttPublisher.PublishChangesAsync(readings);

    Thread.Sleep(TimeSpan.FromMinutes(1));
}