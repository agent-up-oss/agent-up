using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Features.Orchestration.Controllers;

public sealed class OrchestrationConsoleController(OrchestrationConsoleService console)
{
    public Task<McpToolResult> GetConsoleAsync(
        string? id,
        string? worktreePath,
        int lineLimit,
        int auditLimit,
        CancellationToken cancellationToken)
        => console.GetConsoleAsync(id, worktreePath, lineLimit, auditLimit, cancellationToken);
}
