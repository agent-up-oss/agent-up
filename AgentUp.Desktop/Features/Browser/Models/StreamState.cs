namespace AgentUp.Desktop.Features.Browser.Models;

// Wire kinds match StreamStateEvent on the server (kebab-cased type + snake_case kind).
internal enum StreamStateKind
{
    ChromiumDownloading,
    WorkspaceStopped,
    AppConnecting,
    AppFailed,
    SessionLaunching,
    Streaming,
}

internal sealed record StreamStateSnapshot(
    string WorkspaceId,
    StreamStateKind Kind,
    string? ChromiumState,
    int ChromiumProgress,
    int Attempt,
    int MaxAttempts,
    string? Reason,
    string? CurrentUrl);
