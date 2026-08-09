namespace AgentUp.Server.Features.Browser.Models;

// Snapshot of everything WorkspaceStreamStateService needs to derive a StreamState.
// Kept internal to the Browser slice: not exposed on any public API.
internal sealed record WorkspaceStreamInputs
{
    public bool IsRunning { get; init; }
    public IReadOnlyDictionary<string, string> PortHealth { get; init; } = new Dictionary<string, string>();
    public bool SessionActive { get; init; }
    public CurrentStreamTarget? CurrentTarget { get; init; }
}

internal sealed record CurrentStreamTarget(string? AppName, int Port, string Url, bool HealthChecked);
