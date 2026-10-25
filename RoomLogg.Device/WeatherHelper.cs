namespace RoomLogg.Device;

public static class WeatherHelper
{
    public static Temperature CalculateHeatIndex(Temperature airTemperature, RelativeHumidity relativeHumidity)
    {
        var tf = airTemperature.DegreesFahrenheit;
        var rh = relativeHumidity.Percent;
        var tf2 = Math.Pow(tf, 2);
        var rh2 = Math.Pow(rh, 2);

        var steadman = 0.5 * (tf + 61.0 + (tf - 68.0) * 1.2 + rh * 0.094);

        if (steadman + tf < 160.0)
            return Temperature.FromDegreesFahrenheit(steadman);

        var rothfuszRegression = -42.379
                                 + 2.04901523 * tf
                                 + 10.14333127 * rh
                                 - 0.22475541 * tf * rh
                                 - 6.83783e-3 * tf2
                                 - 5.481717e-2 * rh2
                                 + 1.22874e-3 * tf2 * rh
                                 + 8.5282e-4 * tf * rh2
                                 - 1.99e-6 * tf2 * rh2;

        if (rh < 13.0 && tf is >= 80.0 and <= 112.0)
            rothfuszRegression += (13.0 - rh) / 4.0 * Math.Sqrt((17.0 - Math.Abs(tf - 95.0)) / 17.0);
        else if (rh > 85.0 && tf is >= 80.0 and <= 87.0)
            rothfuszRegression += (rh - 85.0) / 10.0 * ((87.0 - tf) / 5.0);

        return Temperature.FromDegreesFahrenheit(rothfuszRegression);
    }

    public static Temperature CalculateDewPoint(Temperature airTemperature, RelativeHumidity relativeHumidity)
    {
        if (relativeHumidity.Percent <= 0.1)
            relativeHumidity = RelativeHumidity.FromPercent(0.1);

        var pa = CalculateActualVaporPressure(airTemperature, relativeHumidity);
        const double a = 6.1121;
        const double b = 18.678;
        const double c = 257.14;
        var dp = c * Math.Log(pa / a) / (b - Math.Log(pa / a));
        return Temperature.FromDegreesCelsius(dp);
    }

    private static double CalculateActualVaporPressure(Temperature airTemperature, RelativeHumidity relativeHumidity) =>
        relativeHumidity.Percent / 100.0 * (CalculateSaturatedVaporPressureOverWater(airTemperature) / 100.0);

    private static double CalculateSaturatedVaporPressureOverWater(Temperature airTemperature)
    {
        var rk = airTemperature.DegreesCelsius + 273.15;
        return Math.Exp(-6094.4642 / rk + 21.1249952 - 2.7245552e-2 * rk + 1.6853396e-5 * rk * rk +
                        2.4575506 * Math.Log(rk));
    }
}