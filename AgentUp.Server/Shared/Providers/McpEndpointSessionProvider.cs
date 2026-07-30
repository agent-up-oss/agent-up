using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Shared.Providers;

public sealed class McpEndpointSessionProvider
{
    private static readonly HashSet<string> CommitTools = new(StringComparer.Ordinal)
    {
        "enqueue_commit",
        "enqueue_review_fix_commit",
        "get_commits_status",
        "guard_commits",
        "get_commit_changes",
        "inspect_commit",
        "update_commit_message",
        "update_commit_tests",
        "add_commit_files",
        "remove_commit_files",
        "remove_commit",
        "restore_commit",
        "clear_commits",
        "begin_commit_edit",
        "save_commit_edit",
        "abort_commit_edit"
    };

    private static readonly HashSet<string> OrchestrationTools = new(StringComparer.Ordinal)
    {
        "start_workspace",
        "stop_workspace",
        "get_workspace_status",
        "list_workspaces",
        "get_agent_up_context",
        "get_agent_up_json_format"
    };

    public Task ConfigureAsync(HttpContext context, McpServerOptions options, CancellationToken cancellationToken)
    {
        if (IsEndpoint(context, "/mcp/commits"))
        {
            options.ServerInstructions = "Agent-Up commit queue MCP server. Use these tools only for commit queue guard, inspection, enqueue, metadata edits, edit sessions, archive, restore, and clear operations.";
            KeepTools(options, CommitTools);
            options.ResourceCollection?.Clear();
        }
        else if (IsEndpoint(context, "/mcp/orchestration"))
        {
            KeepTools(options, OrchestrationTools);
        }

        return Task.CompletedTask;
    }

    private static bool IsEndpoint(HttpContext context, string endpoint)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.Equals(endpoint, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(endpoint + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void KeepTools(McpServerOptions options, HashSet<string> allowedNames)
    {
        var tools = options.ToolCollection;
        if (tools is null)
            return;

        foreach (var tool in tools.ToArray().Where(tool => !allowedNames.Contains(tool.ProtocolTool?.Name ?? string.Empty)))
        {
            tools.Remove(tool);
        }
    }
}
