using System.Text.RegularExpressions;

namespace AgentUp.Server.Features.Validation.Models;

public static partial class MobileSurfaceRoutes
{
    public const string Connect = "/connect";
    public const string Workspaces = "/workspace";
    public const string Workspace = "/workspace/{workspaceId}";
    public const string WorkspaceApplications = "/workspace/{workspaceId}";
    public const string WorkspaceGit = "/workspace/{workspaceId}/git";
    public const string WorkspaceGitReview = "/workspace/{workspaceId}/git/review";
    public const string WorkspaceGitHistory = "/workspace/{workspaceId}/git/history";
    public const string WorkspaceAgents = "/workspace/{workspaceId}/agents";
    public const string WorkspaceAgent = "/workspace/{workspaceId}/agent";
    public const string WorkspaceSettings = "/workspace/{workspaceId}/settings";
    public const string WorkspaceApplication = "/workspace/{workspaceId}/application/{name}";

    public static readonly IReadOnlyList<string> All =
    [
        Connect,
        Workspaces,
        Workspace,
        WorkspaceGit,
        WorkspaceGitReview,
        WorkspaceGitHistory,
        WorkspaceAgents,
        WorkspaceAgent,
        WorkspaceSettings,
        WorkspaceApplication
    ];

    public static bool IsExpoRoute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\', StringComparison.Ordinal))
            return false;

        return ExpoRoutePattern().IsMatch(path);
    }

    [GeneratedRegex(
        @"^/(connect|workspace(/(?:\{workspaceId\}|[^/]+))?(/git(/review|/history)?|/agents|/agent|/settings|/application/(?:\{name\}|[^/]+))?)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExpoRoutePattern();
}
