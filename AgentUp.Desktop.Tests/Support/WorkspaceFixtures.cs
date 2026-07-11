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
}
