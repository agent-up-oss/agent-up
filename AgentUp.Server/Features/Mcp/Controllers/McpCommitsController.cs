using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Services;

namespace AgentUp.Server.Features.Mcp.Controllers;

public sealed class McpCommitsController(McpCommitsService service)
{
    public Task<McpToolResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken)
        => service.EnqueueAsync(worktreePath, request, cancellationToken);

    public Task<McpToolResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken)
        => service.GetStatusAsync(worktreePath, cancellationToken);
}
