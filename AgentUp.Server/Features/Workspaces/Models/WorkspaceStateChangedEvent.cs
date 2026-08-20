namespace AgentUp.Server.Features.Workspaces.Models;

public sealed record WorkspaceStateChangedEvent(
    string WorkspaceId,
    string State,
    IReadOnlyList<AppStateChange> Applications,
    string? HealthState = null);

public sealed record AppStateChange(
    string Name,
    string State,
    IReadOnlyList<PortHealthChange>? PortHealth = null);

public sealed record PortHealthChange(int AllocatedPort, string HealthState);
