namespace AgentUp.Server.Features.Browser.Models;

// Snapshot of everything WorkspaceStreamStateService needs to derive a StreamState.
// Kept internal to the Browser slice: not exposed on any public API.
internal sealed record WorkspaceStreamInputs
{
    public bool IsRunning { get; init; }
    public IReadOnlyDictionary<string, string> PortHealth { get; init; } = new Dictionary<string, string>();
    public bool SessionActive { get; init; }
    public CurrentStreamTarget? CurrentTarget { get; init; }
    // Standalone probe state — only meaningful when CurrentTarget?.HealthChecked == false.
    // Written by the probe task; read by Compute. Publishing through Compute means the
    // probe cannot leak stale AppConnecting after OnWorkspaceStopped (IsRunning=false
    // wins in Compute regardless of what the probe most recently observed).
    public int StandaloneProbeAttempt { get; init; }
    public bool StandaloneProbeReachable { get; init; }
    public bool StandaloneProbeExhausted { get; init; }
}

internal sealed record CurrentStreamTarget(string? AppName, int Port, string Url, bool HealthChecked);
