using System.ComponentModel;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

[McpServerToolType]
public sealed class WorkspaceReviewSnapshotMcpTools(WorkspaceReviewSnapshotController snapshots)
{
    [McpServerTool(Name = "export_workspace_review_snapshot", Title = "Export Workspace Review Snapshot")]
    [Description("Export a shareable snapshot of one workspace after an agent task: identity and revision, allocated ports, running processes and Docker services, health and diagnostics, the open browser URL, recent logs, recent MCP and workspace actions, and screenshot artifact ids. verification is the same export_verification document nested as a section when the worktree can produce it; missing verification does not fail the snapshot. Evidence to share, never a substitute for living on the original machine, and never an input to verify guard.")]
    public async Task<McpToolResult> ExportWorkspaceReviewSnapshot(
        [Description("Registered workspace id.")] string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshots.GetAsync(workspaceId, cancellationToken);
        return snapshot is null
            ? new McpToolResult(false, $"Workspace '{workspaceId}' was not found.")
            : new McpToolResult(true, $"Exported a review snapshot for workspace '{snapshot.DisplayName}'.", snapshot);
    }
}
