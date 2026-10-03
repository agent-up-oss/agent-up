namespace AgentUp.Desktop.Features.Workspaces.Models;

public static class WorkspaceScopeKey
{
    public static string For(string connectionId, string workspaceId)
        => $"{connectionId}:{workspaceId}";
}
