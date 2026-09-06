namespace AgentUp.Server.Features.Workspaces.DTOs;

public sealed record WorkspaceLifecycleResult(bool Found, bool Succeeded, string? Error)
{
    public static WorkspaceLifecycleResult NotFound() => new(false, false, null);

    public static WorkspaceLifecycleResult Success() => new(true, true, null);

    public static WorkspaceLifecycleResult Failed(string error) => new(true, false, error);
}
