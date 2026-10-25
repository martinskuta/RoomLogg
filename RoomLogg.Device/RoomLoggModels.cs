namespace RoomLogg.Device;

public enum TemperatureUnit : byte
{
    DegreeCelsius = 0,
    DegreeFahrenheit = 1
}

public readonly record struct Temperature
{
    public Temperature(double value, TemperatureUnit unit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("Value cannot be NaN or Infinity.", nameof(value));

        Value = value;
        Unit = unit;
    }

    public double Value { get; }
    public TemperatureUnit Unit { get; }

    public double DegreesCelsius => As(TemperatureUnit.DegreeCelsius);
    public double DegreesFahrenheit => As(TemperatureUnit.DegreeFahrenheit);

    public static Temperature FromDegreesCelsius(double degreesCelsius) =>
        new(degreesCelsius, TemperatureUnit.DegreeCelsius);

    public static Temperature FromDegreesFahrenheit(double degreesFahrenheit) =>
        new(degreesFahrenheit, TemperatureUnit.DegreeFahrenheit);

    public double As(TemperatureUnit unit) => (Unit, unit) switch
    {
        (TemperatureUnit.DegreeCelsius, TemperatureUnit.DegreeCelsius) => Value,
        (TemperatureUnit.DegreeFahrenheit, TemperatureUnit.DegreeFahrenheit) => Value,
        (TemperatureUnit.DegreeCelsius, TemperatureUnit.DegreeFahrenheit) => Value * 9.0 / 5.0 + 32.0,
        (TemperatureUnit.DegreeFahrenheit, TemperatureUnit.DegreeCelsius) => (Value - 32.0) * 5.0 / 9.0,
        _ => throw new NotImplementedException($"Can not convert {Unit} to {unit}.")
    };

    public Temperature ToUnit(TemperatureUnit unit) =>
        Unit == unit ? this : new Temperature(As(unit), unit);
}

public readonly record struct RelativeHumidity
{
    public RelativeHumidity(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
            throw new ArgumentException("Value cannot be NaN or Infinity.", nameof(percent));

        Percent = percent;
    }

    public double Value => Percent;
    public double Percent { get; }

    public static RelativeHumidity FromPercent(double percent) => new(percent);
}

public enum GraphType : byte
{
    Temperature = 0,
    Humidity = 1,
    DewPoint = 2,
    HeatIndex = 3
}

public enum TimeDisplayFormat : byte
{
    TwentyFourHour = 0, // h:mm:ss
    TwelveHourAmPrefix = 1, // AM h:mm:ss
    TwelveHourAmSuffix = 2 // h:mm:ss AM
}

public enum DateDisplayFormat : byte
{
    DayMonthYear = 0, // DD-MM-YYYY
    MonthDayYear = 1, // MM-DD-YYYY
    YearMonthDay = 2 // YYYY-MM-DD
}

public enum DeviceLanguage : byte
{
    German = 0,
    English = 1
}

[Flags]
public enum DisplayChannelMetrics : byte
{
    None = 0,
    Temperature = 1 << 0,
    DewPoint = 1 << 1,
    HeatIndex = 1 << 2,
    All = Temperature | DewPoint | HeatIndex
}

public readonly record struct SensorReading(
    int Channel,
    bool IsConnected,
    Temperature? Temperature,
    RelativeHumidity? Humidity,
    Temperature? DewPoint,
    Temperature? HeatIndex)
{
    public static SensorReading Disconnected(int channel) => new(channel, false, null, null, null, null);

    public static SensorReading Connected(int channel, Temperature temperature, RelativeHumidity humidity,
        Temperature dewPoint, Temperature heatIndex) => new(channel, true, temperature, humidity, dewPoint, heatIndex);
}

public sealed record DeviceConfiguration(
    GraphType GraphType,
    byte GraphHours,
    TimeDisplayFormat TimeFormat,
    DateDisplayFormat DateFormat,
    bool DaylightSavingTime,
    sbyte TimeZoneOffset,
    TemperatureUnit TemperatureUnit,
    uint[] DisplayAreas)
{
    public static DisplayChannelMetrics GetChannelMetrics(uint areaMask, int channel)
    {
        if (channel is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 1 and 8.");

        var shift = (channel - 1) * 3;
        return (DisplayChannelMetrics)((areaMask >> shift) & 0b111);
    }

    public static uint SetChannelMetrics(uint areaMask, int channel, DisplayChannelMetrics metrics)
    {
        if (channel is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 1 and 8.");

        var shift = (channel - 1) * 3;
        var cleared = areaMask & ~(0b111u << shift);
        return cleared | (((uint)metrics & 0b111u) << shift);
    }
}

public readonly record struct ChannelCalibration(
    int Channel,
    double? TemperatureOffsetCelsius,
    int? HumidityOffsetPercent);

public readonly record struct AlarmStatus(
    bool BuzzerEnabled,
    bool IceAlarmEnabled,
    byte HighTemperatureMask,
    byte LowTemperatureMask,
    byte HighHumidityMask,
    byte LowHumidityMask)
{
    public bool IsHighTemperatureEnabled(int channel)
    {
        return IsChannelBitSet(HighTemperatureMask, channel);
    }

    public bool IsLowTemperatureEnabled(int channel)
    {
        return IsChannelBitSet(LowTemperatureMask, channel);
    }

    public bool IsHighHumidityEnabled(int channel)
    {
        return IsChannelBitSet(HighHumidityMask, channel);
    }

    public bool IsLowHumidityEnabled(int channel)
    {
        return IsChannelBitSet(LowHumidityMask, channel);
    }

    private static bool IsChannelBitSet(byte mask, int channel)
    {
        if (channel is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 1 and 8.");

        return (mask & (1 << (channel - 1))) != 0;
    }
}

public readonly record struct ChannelTemperatureAlarm(
    int Channel,
    double? HighTemperatureCelsius,
    double? LowTemperatureCelsius);

public readonly record struct ChannelHumidityAlarm(
    int Channel,
    int? HighHumidityPercent,
    int? LowHumidityPercent);