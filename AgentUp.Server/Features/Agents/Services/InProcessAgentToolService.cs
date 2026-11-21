using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Authentication.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// The tool surface of an in-process agent. It is the Agent-Up MCP toolset narrowed three ways,
/// each only removing: to tools that name a workspace (so the call can be pinned), to tools the
/// team member's grant allows (through the same operation-permission map tokens use), and to
/// that one workspace. The task tools on <c>/mcp/tasks</c> are among them, so a reviewer submits
/// its review exactly as a coding agent does; a submission that lands ends the task. One host
/// tool sits beside them: <c>complete_task</c>, which ends the loop task.
/// </summary>
public sealed class InProcessAgentToolService(
    ILoopbackMcpClient mcp,
    InProcessToolCatalogProvider catalog,
    TeamMemberService members,
    McpToolPermissionController permissions) : IInProcessToolHost
{
    public const string CompleteTask = "complete_task";
    /// <summary>The task tools whose success ends the member's task: the Server takes it from there.</summary>
    private static readonly IReadOnlySet<string> Submissions =
        new HashSet<string>(StringComparer.Ordinal) { "submit_review", "submit_work" };

    /// <summary>The MCP endpoints an in-process agent draws tools from. Code endpoints are absent on purpose.</summary>
    public static IReadOnlyList<string> Endpoints { get; } = ["/mcp/tasks", "/mcp/browser", "/mcp/orchestration"];

    public async Task<IReadOnlyList<InProcessTool>> ListAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var granted = members.Grants(workspaceId);
        var tools = new List<InProcessTool>();
        foreach (var endpoint in Endpoints)
        {
            foreach (var descriptor in await mcp.ListToolsAsync(endpoint, cancellationToken))
            {
                var tool = catalog.Describe(descriptor);
                if (tool is not null && Allowed(granted, tool.Name))
                    tools.Add(tool);
            }
        }

        tools.Add(CompleteTaskTool);
        return tools;
    }

    public async Task<InProcessToolResult> CallAsync(
        string workspaceId,
        string tool,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (tool == CompleteTask)
            return new InProcessToolResult("Task marked complete.", [], false, CompletesTask: true);

        var granted = members.Grants(workspaceId);
        if (!Allowed(granted, tool))
            return Refused($"Tool '{tool}' is outside what a {granted.Kind} team member may use.");
        var endpoint = await EndpointOfAsync(tool, cancellationToken);
        if (endpoint is null)
            return Refused($"Tool '{tool}' is not offered to in-process agents.");
        var result = catalog.Read(await mcp.CallToolAsync(endpoint, tool, catalog.Pin(arguments, workspaceId), cancellationToken));
        return Submissions.Contains(tool) && !result.IsError && Succeeded(result.Text) ? result with { CompletesTask = true } : result;
    }

    /// <summary>A task tool answers with whether the Server took the submission; a refusal leaves the task open.</summary>
    private static bool Succeeded(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("succeeded", out var succeeded)
                && succeeded.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool Allowed(TeamMemberGrants granted, string tool)
    {
        var required = permissions.RequiredPermission(tool);
        return required is not null && granted.OperationPermissions.Contains(required);
    }

    private async Task<string?> EndpointOfAsync(string tool, CancellationToken cancellationToken)
    {
        foreach (var endpoint in Endpoints)
        {
            var offered = await mcp.ListToolsAsync(endpoint, cancellationToken);
            if (offered.Select(catalog.Describe).Any(item => item?.Name == tool))
                return endpoint;
        }

        return null;
    }

    private static InProcessToolResult Refused(string reason) => new(reason, [], true);

    private static readonly InProcessTool CompleteTaskTool = new(
        CompleteTask,
        "Call once the assigned task is finished. Ends this task in the Agent-Up loop.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { summary = new { type = "string", description = "One-paragraph outcome." } }
        }));

}
