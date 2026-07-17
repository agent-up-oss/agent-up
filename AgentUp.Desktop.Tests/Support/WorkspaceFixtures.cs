using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;

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
                new ApplicationDto("API", "cargo run", null, "Running"),
                new ApplicationDto("Docs", "npm run start", "docs", "Running"),
            ]
        };

    public static WorkspaceDto WithHttpPort(string id = "ws-1", int port = 3000) =>
        new(id, id, $"/repo/{id}", $"/worktrees/{id}", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, port, port)]
                }
            ]
        };

    public static Dictionary<string, List<string>> OutputFor(
        string workspaceId, string appName, List<string> lines) =>
        new() { [$"{workspaceId}/{appName}"] = lines };
}
