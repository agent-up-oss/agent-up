using System.Text.Json;

namespace AgentUp.Desktop.Features.Browser.Models;

// Immutable snapshot of the JS state machine, produced by parsing the JSON returned by
// window.__viewer.snapshot(). Used as Avalonia's only input from the viewer JS layer.
// ObservedAt is UTC-side and lets us tell how stale a snapshot is if polling stops.
internal sealed record ViewerSnapshot(
    string State,
    long Since,
    int FramesReceived,
    long LastFrameAt,
    string WsReadyState,
    string Presence,
    string PageInstanceId,
    bool ServerReportedActive,
    DateTimeOffset ObservedAt)
{
    // Age of the current JS SM state at the moment the snapshot was taken.
    // JS reports `since` as Date.now() ms. We derive age from the JS-side clock so it's
    // immune to clock skew between JS and Avalonia.
    public TimeSpan StateAge { get; init; } = TimeSpan.FromMilliseconds(Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Since));

    public static ViewerSnapshot? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "null") return null;
        try
        {
            // NativeWebView.InvokeScript often wraps the JSON in quotes (returning a
            // literal string result). Handle both a bare JSON object and a JSON string
            // containing JSON.
            var text = raw.TrimStart();
            if (text.StartsWith('"'))
                text = JsonSerializer.Deserialize<string>(text) ?? "";
            if (string.IsNullOrWhiteSpace(text) || text == "null") return null;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var since = root.TryGetProperty("since", out var sinceEl) ? sinceEl.GetInt64() : 0L;
            return new ViewerSnapshot(
                State: root.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "",
                Since: since,
                FramesReceived: root.TryGetProperty("framesReceived", out var fr) ? fr.GetInt32() : 0,
                LastFrameAt: root.TryGetProperty("lastFrameAt", out var lfa) ? lfa.GetInt64() : 0L,
                WsReadyState: root.TryGetProperty("wsReadyState", out var ws) ? ws.GetString() ?? "" : "",
                Presence: root.TryGetProperty("presence", out var p) ? p.GetString() ?? "" : "",
                PageInstanceId: root.TryGetProperty("pageInstanceId", out var pid) ? pid.GetString() ?? "" : "",
                ServerReportedActive: root.TryGetProperty("serverReportedActive", out var sra) && sra.ValueKind == JsonValueKind.True,
                ObservedAt: DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
