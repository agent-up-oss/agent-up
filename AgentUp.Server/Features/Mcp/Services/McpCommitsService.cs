using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Mcp.DTOs;

namespace AgentUp.Server.Features.Mcp.Services;

public sealed class McpCommitsService(CommitsController commits)
{
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

        CommitsEnqueueResult result;
        try
        {
            result = await commits.EnqueueAsync(worktreePath, request, cancellationToken);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }

        if (!result.Succeeded && result.Message.StartsWith("Queue operation failed:", StringComparison.Ordinal))
            return new McpToolResult(false, "Commit queue operation failed.");

        return new McpToolResult(result.Succeeded, result.Message);
    }

    public async Task<McpToolResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        CommitsStatusResult status;
        try
        {
            status = await commits.GetStatusAsync(worktreePath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }

        var message = status.Entries.Count == 0
            ? "No queued commit entries."
            : $"{status.Entries.Count} queued entr{(status.Entries.Count == 1 ? "y" : "ies")}.";
        return new McpToolResult(true, message, status);
    }
}
