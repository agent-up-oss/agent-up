namespace AgentUp.Desktop.Features.Workspaces.DTOs;

public static class WorkspaceListOrdering
{
    public static bool IsActive(string? state) =>
        state is "Running" or "Degraded" or "Starting";

    public static int ActivePriority(string? state) => state switch
    {
        "Running" or "Degraded" => 2,
        "Starting" => 1,
        _ => 0
    };

    public static int OwnWorkspacePriority(bool derived) => derived ? 0 : 1;
}
