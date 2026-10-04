using System.Net;
using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        "add_commit_files",
        "remove_commit_files",
        "remove_commit",
        "restore_commit",
        "clear_commits",
        "begin_commit_edit",
        "save_commit_edit",
        "abort_commit_edit"
    };

    private static readonly HashSet<string> VerificationTools = new(StringComparer.Ordinal)
    {
        "plan_verification",
        "run_verification",
        "run_verification_check",
        "guard_verification"
    };

    private static readonly HashSet<string> OrchestrationTools = new(StringComparer.Ordinal)
    {
        "start_workspace",
        "stop_workspace",
        "get_workspace_status",
        "list_workspaces",
        "get_workspace_console",
        "get_workspace_diagnostics",
        "get_agent_up_context",
        "get_agent_up_json_format"
    };

    private static readonly HashSet<string> BrowserTools = new(StringComparer.Ordinal)
    {
        "browser_navigate",
        "browser_inspect",
        "browser_click",
        "browser_fill",
        "browser_press",
        "browser_wait_for_selector",
        "browser_wait_for_text",
        "browser_wait_for_navigation",
        "browser_screenshot",
        "desktop_inspect",
        "desktop_click",
        "desktop_fill",
        "desktop_press",
        "desktop_screenshot",
        "save_validation_flow",
        "list_validation_flows",
        "play_validation_flow",
        "export_validation_flow",
        "delete_validation_flow"
    };

    private static readonly HashSet<string> AuditTools = new(StringComparer.Ordinal)
    {
        "audit_query",
        "audit_timeline",
        "audit_get_event",
        "audit_load_artifact"
    };

    private static readonly HashSet<string> CapabilityTools = new(StringComparer.Ordinal)
    {
        "list_capability_modules",
        "enable_capability_module",
        "disable_capability_module"
    };

    private const string CommitsInstructions =
        "Agent-Up commit queue MCP server. Use these tools only for commit queue guard, inspection, enqueue, metadata edits, edit sessions, archive, restore, and clear operations.";

    private const string VerificationInstructions =
        "Agent-Up verification MCP server. Test selection is owned by the static path rules in agent-up.json, not by you: plan_verification shows what the current changes require and you cannot narrow it. Run run_verification at the end of a task, before enqueueing commits, so receipts cover the code while it is still in the working tree. guard_verification reports whether every required check has a passing receipt matching the current file contents.";

    private const string BrowserInstructions =
        "Agent-Up browser MCP server. Use these tools to navigate, inspect, and interact with the workspace browser the user watches in Desktop. When recording validation flows, start from the user's goal: infer routes, tabs, and labels from application source or router files when that is faster than live inspection, then perform the journey once and call save_validation_flow. Do not inspect every route before recording. Saved flows replay with staged mouse movement, half-second attention pings, navigation waits, and visible expectations.";

    private const string AuditInstructions =
        "Agent-Up audit MCP server. Use these tools to query durable workspace, browser, MCP, process, source revision, health, and artifact history.";

    private const string CapabilitiesInstructions =
        "Agent-Up capability modules MCP server. List, enable, and disable registry packages on this Server. Enabling wraps later application and agent launches through nix. Desktop and Mobile never call the remote registry.";

    /// <summary>
    /// Three narrowings compose here, and the order is what keeps any one of them from widening
    /// another: the endpoint decides which slice a session serves, the caller's permissions
    /// remove what its token does not grant, and the workspace pin binds what is left to one
    /// workspace. Each step only ever removes from what the previous step produced.
    /// </summary>
    public Task ConfigureAsync(HttpContext context, McpServerOptions options, CancellationToken cancellationToken)
    {
        var binding = context.RequestServices.GetService<McpWorkspaceBindingController>();
        var instructions = InstructionContext(context, binding?.BoundWorkspace(context));
        ConfigureEndpoint(context, options, instructions);
        context.RequestServices.GetService<McpToolPermissionController>()?.Restrict(context, options);
        binding?.Pin(context, options);
        return Task.CompletedTask;
    }

    private static void ConfigureEndpoint(HttpContext context, McpServerOptions options, McpInstructionContext instructions)
    {
        if (IsEndpoint(context, "/mcp/commits"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.ForEndpoint(CommitsInstructions, instructions);
            KeepTools(options, CommitTools);
            options.ResourceCollection?.Clear();
        }
        else if (IsEndpoint(context, "/mcp/verification"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.ForEndpoint(VerificationInstructions, instructions);
            KeepTools(options, VerificationTools);
            options.ResourceCollection?.Clear();
        }
        else if (IsEndpoint(context, "/mcp/orchestration"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.Build(instructions);
            KeepTools(options, OrchestrationTools);
        }
        else if (IsEndpoint(context, "/mcp/browser"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.ForEndpoint(BrowserInstructions, instructions);
            KeepTools(options, BrowserTools);
            options.ResourceCollection?.Clear();
        }
        else if (IsEndpoint(context, "/mcp/audit"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.ForEndpoint(AuditInstructions, instructions);
            KeepTools(options, AuditTools);
            options.ResourceCollection?.Clear();
        }
        else if (IsEndpoint(context, "/mcp/capabilities"))
        {
            options.ServerInstructions = AgentUpMcpGuidance.ForEndpoint(CapabilitiesInstructions, instructions);
            KeepTools(options, CapabilityTools);
            options.ResourceCollection?.Clear();
        }
        else
        {
            options.ServerInstructions = AgentUpMcpGuidance.Build(instructions);
        }
    }

    /// <summary>
    /// A pinned session has no workspace to choose, and a caller that reached this Server across
    /// the network cannot open the paths the Server opens. Everything else is a client sharing
    /// the Server's host, which is what the path-first advice has always assumed.
    /// </summary>
    public static McpInstructionContext InstructionContext(HttpContext context, string? boundWorkspace)
    {
        if (!string.IsNullOrWhiteSpace(boundWorkspace))
            return McpInstructionContext.Pinned(boundWorkspace);

        return SharesServerHost(context.Connection.RemoteIpAddress)
            ? McpInstructionContext.SharedFilesystem
            : McpInstructionContext.Remote;
    }

    private static bool SharesServerHost(IPAddress? remote)
        => remote is null || IPAddress.IsLoopback(remote);

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
