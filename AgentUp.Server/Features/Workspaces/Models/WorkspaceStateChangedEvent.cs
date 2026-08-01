namespace AgentUp.Server.Features.Workspaces.Models;

public sealed record WorkspaceStateChangedEvent(
    string WorkspaceId,
    string State,
    IReadOnlyList<AppStateChange> Applications);

public sealed record AppStateChange(string Name, string State);
