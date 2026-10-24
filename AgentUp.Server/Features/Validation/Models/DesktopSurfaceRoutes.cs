namespace AgentUp.Server.Features.Validation.Models;

public static class DesktopSurfaceRoutes
{
    public const string Login = "login";
    public const string WorkspacesList = "workspaces/list";
    public const string WorkspaceOverview = "workspace/overview";
    public const string WorkspaceApplications = "workspace/applications";
    public const string WorkspaceGit = "workspace/git";
    public const string WorkspaceAgents = "workspace/agents";

    public static readonly IReadOnlyList<string> All =
    [
        Login,
        WorkspacesList,
        WorkspaceOverview,
        WorkspaceApplications,
        WorkspaceGit,
        WorkspaceAgents
    ];

    public static bool IsNamedRoute(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && All.Contains(path, StringComparer.Ordinal);
}
