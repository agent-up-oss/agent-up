using System.Globalization;

namespace AgentUp.Desktop.Features.Metrics.Views;

internal static class MetricsAxisScaler
{
    public static (double Max, double Step) Compute(double dataMax, int targetTicks = 5)
    {
        if (dataMax <= 0)
            return (1, 0.25);

        var rawStep = dataMax / targetTicks;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var niceNormalized = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        var step = niceNormalized * magnitude;
        var niceMax = Math.Ceiling(dataMax / step) * step;
        if (niceMax < step)
            niceMax = step;

        return (niceMax, step);
    }

    public static string FormatTick(double value, string unit)
    {
        if (unit == "%")
            return value.ToString("0", CultureInfo.InvariantCulture) + "%";

        if (unit == "ms")
            return value >= 1000
                ? (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "s"
                : value.ToString("0", CultureInfo.InvariantCulture);

        if (unit == "bytes")
            return value >= 1_048_576
                ? (value / 1_048_576).ToString("0.#", CultureInfo.InvariantCulture) + " MB"
                : value >= 1024
                    ? (value / 1024).ToString("0.#", CultureInfo.InvariantCulture) + " KB"
                    : value.ToString("0", CultureInfo.InvariantCulture);

        if (value >= 1_000_000)
            return (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";

        if (value >= 1_000)
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k";

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
