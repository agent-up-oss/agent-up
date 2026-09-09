using System.ComponentModel;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

[McpServerToolType]
public sealed class DiagnosticsMcpTools(WorkspaceDiagnosticsController diagnostics)
{
    [McpServerTool(Name = "get_workspace_diagnostics", Title = "Get Workspace Diagnostics")]
    [Description("Return one workspace's process state, application logs, health, JavaScript exceptions, failed network requests, and browser errors. Entries identify their application or browser session and distinguish active from resolved outcomes.")]
    public async Task<McpToolResult> GetWorkspaceDiagnostics(
        [Description("Registered workspace id.")] string workspaceId,
        [Description("Optional application name filter.")] string? application = null,
        [Description("Maximum recent log lines per application (1-1000).") ] int logLimit = 200,
        [Description("Maximum diagnostic entries (1-500).") ] int entryLimit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await diagnostics.GetAsync(workspaceId, application, logLimit, entryLimit, cancellationToken);
        return result is null
            ? new McpToolResult(false, $"Workspace '{workspaceId}' was not found.")
            : new McpToolResult(true, $"Returned diagnostics for workspace '{result.WorkspaceName}'.", result);
    }
}
