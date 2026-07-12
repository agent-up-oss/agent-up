using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.Workspaces.Http;

namespace AgentUp.Desktop.Tests.Support;

internal static class WorkspaceFixtures
{
    public static WorkspaceDto Single() =>
        new("ws-1", "My App", "/repo/my-app", "/worktrees/my-app", "feat/dashboard", "abc123", "Running");

    public static List<WorkspaceDto> Multiple() =>
    [
        new("ws-1", "My App", "/repo/my-app", "/worktrees/my-app", "feat/dashboard", "abc123", "Running"),
        new("ws-2", "Auth Service", "/repo/auth", "/worktrees/auth", "fix/token-refresh", "def456", "Stopped"),
        new("ws-3", "API Gateway", "/repo/api", "/worktrees/api", "main", "ghi789", "Running"),
    ];

    public static WorkspaceDto WithApplications() =>
        new("ws-1", "My App", "/repo/my-app", "/worktrees/my-app", "feat/dashboard", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("API", "cargo run", null, null, "Running"),
                new ApplicationDto("Docs", "npm run start", "docs", null, "Running"),
            ]
        };

    public static Dictionary<string, List<string>> OutputFor(
        string workspaceId, string appName, List<string> lines) =>
        new() { [$"{workspaceId}/{appName}"] = lines };
}
