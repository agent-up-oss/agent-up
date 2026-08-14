using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentUp.Browser.Streaming.Models;

// Single source of truth for what the desktop must show for a workspace's AI-mode port tab.
// The desktop maps StreamKind → banner or WebView. WebView is visible iff StreamKind == Streaming.
public enum StreamKind
{
    // Chromium binary is not yet available (downloading, not started, or failed).
    ChromiumDownloading,

    // Workspace is not in the Running state (stopped, stopping, starting).
    WorkspaceStopped,

    // Workspace running; app HTTP endpoint not yet reachable / not healthy. Retry in progress.
    AppConnecting,

    // App reachability probe exhausted retries or app reports unhealthy.
    AppFailed,

    // App healthy; Puppeteer session not yet created or waiting for first RDP frame.
    SessionLaunching,

    // Session live, first frame delivered — WebView may be shown.
    Streaming,
}

public sealed record StreamState(
    StreamKind Kind,
    string ChromiumState = "ready",
    int ChromiumProgress = 100,
    int Attempt = 0,
    int MaxAttempts = 0,
    string? Reason = null)
{
    public static StreamState Chromium(string state, int progress) =>
        new(StreamKind.ChromiumDownloading, ChromiumState: state, ChromiumProgress: progress);

    public static StreamState Stopped() => new(StreamKind.WorkspaceStopped);

    public static StreamState Connecting(int attempt, int maxAttempts) =>
        new(StreamKind.AppConnecting, Attempt: attempt, MaxAttempts: maxAttempts);

    public static StreamState Failed(string reason, int maxAttempts) =>
        new(StreamKind.AppFailed, Reason: reason, MaxAttempts: maxAttempts);

    public static StreamState Launching() => new(StreamKind.SessionLaunching);

    public static StreamState Streaming() => new(StreamKind.Streaming);
}

// Wire format for the SSE `stream-state` event. Flat by design so the desktop
// (or any consumer) doesn't need polymorphic JSON. Fields absent for a given
// Kind are serialized as their defaults; consumers ignore what they don't need.
public sealed record StreamStateEvent(
    [property: JsonPropertyName("workspaceId")] string WorkspaceId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("chromiumState")] string? ChromiumState,
    [property: JsonPropertyName("chromiumProgress")] int ChromiumProgress,
    [property: JsonPropertyName("attempt")] int Attempt,
    [property: JsonPropertyName("maxAttempts")] int MaxAttempts,
    [property: JsonPropertyName("reason")] string? Reason)
{
    [JsonPropertyName("type")] public string Type => "stream-state";

    public static StreamStateEvent From(string workspaceId, StreamState state) => new(
        WorkspaceId: workspaceId,
        Kind: KindToWire(state.Kind),
        ChromiumState: state.Kind == StreamKind.ChromiumDownloading ? state.ChromiumState : null,
        ChromiumProgress: state.Kind == StreamKind.ChromiumDownloading ? state.ChromiumProgress : 0,
        Attempt: state.Kind == StreamKind.AppConnecting ? state.Attempt : 0,
        MaxAttempts: state.MaxAttempts,
        Reason: state.Reason);

    public string Serialize() => JsonSerializer.Serialize(this, EventJsonOptions);

    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static string KindToWire(StreamKind kind) => kind switch
    {
        StreamKind.ChromiumDownloading => "chromium_downloading",
        StreamKind.WorkspaceStopped => "workspace_stopped",
        StreamKind.AppConnecting => "app_connecting",
        StreamKind.AppFailed => "app_failed",
        StreamKind.SessionLaunching => "session_launching",
        StreamKind.Streaming => "streaming",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown StreamKind"),
    };
}
