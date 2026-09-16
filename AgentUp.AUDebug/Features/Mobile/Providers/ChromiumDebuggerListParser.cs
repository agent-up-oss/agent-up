using System.Text.Json;

namespace AgentUp.AUDebug.Features.Mobile.Providers;

public static class ChromiumDebuggerListParser
{
    public static string? ReadWebSocketUrl(string json)
    {
        using var document = JsonDocument.Parse(json);
        var page = document.RootElement.EnumerateArray()
            .FirstOrDefault(element => element.ValueKind == JsonValueKind.Object
                                       && element.TryGetProperty("webSocketDebuggerUrl", out var url)
                                       && url.ValueKind == JsonValueKind.String
                                       && url.GetString() is { Length: > 0 });
        return page.ValueKind == JsonValueKind.Object
            ? page.GetProperty("webSocketDebuggerUrl").GetString()
            : null;
    }
}
