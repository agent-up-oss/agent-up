using System.Text.Json;

namespace AgentUp.Server.Features.Applications.Providers;

public static class MetricsResponseParser
{
    public static IReadOnlyDictionary<string, string> Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new Dictionary<string, string>();

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>();

            if (document.RootElement.TryGetProperty("metrics", out var metrics)
                && metrics.ValueKind == JsonValueKind.Object)
                return FlattenObject(metrics, "metric.");

            return FlattenObject(document.RootElement, "metric.");
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static Dictionary<string, string> FlattenObject(JsonElement element, string prefix)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix + property.Name;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    result[key] = property.Value.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    result[key] = property.Value.GetRawText();
                    break;
                case JsonValueKind.Object:
                    foreach (var nested in FlattenObject(property.Value, key + "."))
                        result[nested.Key] = nested.Value;
                    break;
            }
        }

        return result;
    }
}
