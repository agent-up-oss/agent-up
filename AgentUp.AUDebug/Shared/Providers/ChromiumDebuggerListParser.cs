using System.Text.Json;

namespace AgentUp.AUDebug.Shared.Providers;

public static class ChromiumDebuggerListParser
{
    public static string? ReadWebSocketUrl(string json, string? urlContains = null)
    {
        using var document = JsonDocument.Parse(json);
        var pages = document.RootElement.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object
                              && element.TryGetProperty("webSocketDebuggerUrl", out var url)
                              && url.ValueKind == JsonValueKind.String
                              && url.GetString() is { Length: > 0 })
            .ToArray();
        var match = urlContains is { Length: > 0 }
            ? pages.FirstOrDefault(element =>
                element.TryGetProperty("url", out var pageUrl)
                && pageUrl.GetString()?.Contains(urlContains, StringComparison.Ordinal) == true)
            : default;
        var page = match.ValueKind == JsonValueKind.Object
            ? match
            : pages.FirstOrDefault();
        return page.ValueKind == JsonValueKind.Object
            ? page.GetProperty("webSocketDebuggerUrl").GetString()
            : null;
    }
}
