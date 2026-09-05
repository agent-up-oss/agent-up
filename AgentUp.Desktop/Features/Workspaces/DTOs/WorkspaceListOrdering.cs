namespace AgentUp.Desktop.Features.Workspaces.DTOs;

public static class WorkspaceListOrdering
{
    public static bool IsActive(string? state) =>
        state is "Running" or "Starting";

    public static int ActivePriority(string? state) => state switch
    {
        "Running" => 2,
        "Starting" => 1,
        _ => 0
    };
}
