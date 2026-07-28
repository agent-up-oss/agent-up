using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Features.Orchestration.Controllers;

public sealed class OrchestrationWorkspaceController
{
    private readonly OrchestrationWorkspaceService _workspaces;

    public OrchestrationWorkspaceController(OrchestrationWorkspaceService workspaces) => _workspaces = workspaces;

    public Task<McpToolResult> StartAsync(string worktreePath, CancellationToken cancellationToken) =>
        _workspaces.StartAsync(worktreePath, cancellationToken);

    public Task<McpToolResult> StopAsync(string? id, string? worktreePath) =>
        _workspaces.StopAsync(id, worktreePath);

    public McpToolResult GetStatus(string? id, string? worktreePath) =>
        _workspaces.GetStatus(id, worktreePath);

    public IReadOnlyList<Workspace> List() => _workspaces.List();

    public Workspace? GetById(string id) => _workspaces.GetById(id);
}
