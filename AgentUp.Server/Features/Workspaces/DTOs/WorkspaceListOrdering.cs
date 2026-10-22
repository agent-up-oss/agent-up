namespace AgentUp.Server.Features.Workspaces.DTOs;

public static class WorkspaceListOrdering
{
    public static bool IsActive(WorkspaceState state) =>
        state is WorkspaceState.Running or WorkspaceState.Degraded or WorkspaceState.Starting;

    public static int ActivePriority(WorkspaceState state) => state switch
    {
        WorkspaceState.Running or WorkspaceState.Degraded => 2,
        WorkspaceState.Starting => 1,
        _ => 0
    };

    public static int OwnWorkspacePriority(bool derived) => derived ? 0 : 1;
}
