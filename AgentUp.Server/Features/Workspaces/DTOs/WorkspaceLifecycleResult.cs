namespace AgentUp.Server.Features.Workspaces.DTOs;

public sealed record WorkspaceLifecycleResult(bool Found, bool Succeeded, string? Error)
{
    public WorkspaceState? State { get; init; }

    public IReadOnlyList<WorkspaceApplicationFailure> Failures { get; init; } = [];

    public static WorkspaceLifecycleResult NotFound() => new(false, false, null);

    public static WorkspaceLifecycleResult Success(
        WorkspaceState state = WorkspaceState.Running,
        IReadOnlyList<WorkspaceApplicationFailure>? failures = null)
        => new(true, true, null)
        {
            State = state,
            Failures = failures ?? []
        };

    public static WorkspaceLifecycleResult Failed(
        string error,
        IReadOnlyList<WorkspaceApplicationFailure>? failures = null)
        => new(true, false, error)
        {
            State = WorkspaceState.Failed,
            Failures = failures ?? []
        };
}
