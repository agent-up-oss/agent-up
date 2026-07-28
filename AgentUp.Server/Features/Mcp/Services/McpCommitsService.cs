using System.Text.Json;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Mcp.DTOs;

namespace AgentUp.Server.Features.Mcp.Services;

public sealed class McpCommitsService(CommitsController commits)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<McpToolResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");
        if (string.IsNullOrWhiteSpace(request.Slice))
            return new McpToolResult(false, "slice is required.");
        if (string.IsNullOrWhiteSpace(request.Message))
            return new McpToolResult(false, "message is required.");
        if (request.Files.Count == 0)
            return new McpToolResult(false, "At least one file is required.");

        var result = await commits.EnqueueAsync(worktreePath, request, cancellationToken);
        return new McpToolResult(result.Succeeded, result.Message);
    }

    public async Task<McpToolResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        var status = await commits.GetStatusAsync(worktreePath, cancellationToken);
        var message = status.Entries.Count == 0
            ? "No queued commit entries."
            : $"{status.Entries.Count} queued entr{(status.Entries.Count == 1 ? "y" : "ies")}.";
        return new McpToolResult(true, message, status);
    }
}
