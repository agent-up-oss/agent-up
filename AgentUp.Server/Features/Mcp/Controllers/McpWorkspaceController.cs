using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Mcp.Controllers;

public sealed class McpWorkspaceController
{
    private readonly McpWorkspaceService _workspaces;

    public McpWorkspaceController(McpWorkspaceService workspaces) => _workspaces = workspaces;

    public Task<McpToolResult> StartAsync(string worktreePath, CancellationToken cancellationToken) =>
        _workspaces.StartAsync(worktreePath, cancellationToken);

    public Task<McpToolResult> StopAsync(string? id, string? worktreePath) =>
        _workspaces.StopAsync(id, worktreePath);

    public McpToolResult GetStatus(string? id, string? worktreePath) =>
        _workspaces.GetStatus(id, worktreePath);

    public IReadOnlyList<Workspace> List() => _workspaces.List();

    public Workspace? GetById(string id) => _workspaces.GetById(id);
}
