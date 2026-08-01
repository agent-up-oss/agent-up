using System.ComponentModel;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Orchestration.Controllers;

[McpServerToolType]
public sealed class OrchestrationMcpTools
{
    private readonly OrchestrationWorkspaceController _workspaces;
    private readonly OrchestrationContextController _context;
    private readonly OrchestrationConsoleController _console;

    public OrchestrationMcpTools(
        OrchestrationWorkspaceController workspaces,
        OrchestrationContextController context,
        OrchestrationConsoleController console)
    {
        _workspaces = workspaces;
        _context = context;
        _console = console;
    }

    [McpServerTool(Name = "start_workspace", Title = "Start Workspace")]
    [Description("Use when the user asks to use Agent-Up to deploy, run, start, launch, serve, bring up, or open an app/workspace. Registers or updates the workspace from agent-up.json, then starts it through Agent-Up Server. Agent-Up starts local development environments; it does not deploy to cloud infrastructure.")]
    public Task<McpToolResult> StartWorkspace(
        [Description("Absolute path to the current repository, workspace, or worktree containing agent-up.json.")] string worktreePath,
        CancellationToken cancellationToken) =>
        _workspaces.StartAsync(worktreePath, cancellationToken);

    [McpServerTool(Name = "stop_workspace", Title = "Stop Workspace")]
    [Description("Use when the user asks Agent-Up to stop, shut down, terminate, or cleanly halt a managed workspace. Stops a registered workspace by workspace id or worktree path.")]
    public Task<McpToolResult> StopWorkspace(
        [Description("Registered workspace id. Optional when worktreePath is supplied.")] string? id = null,
        [Description("Absolute path to a registered workspace or worktree. Optional when id is supplied.")] string? worktreePath = null) =>
        _workspaces.StopAsync(id, worktreePath);

    [McpServerTool(Name = "get_workspace_status", Title = "Get Workspace Status")]
    [Description("Use when the user asks whether Agent-Up is running an app/workspace, which ports were allocated, or what workspace state Agent-Up currently owns. Returns one workspace status when an id or worktree path is supplied; otherwise returns all workspace statuses.")]
    public McpToolResult GetWorkspaceStatus(
        [Description("Registered workspace id. Optional.")] string? id = null,
        [Description("Absolute path to a registered workspace or worktree. Optional.")] string? worktreePath = null) =>
        _workspaces.GetStatus(id, worktreePath);

    [McpServerTool(Name = "list_workspaces", Title = "List Workspaces")]
    [Description("Use when selecting an existing Agent-Up workspace or checking what Agent-Up already knows about before starting, stopping, or inspecting status. Lists all workspaces registered with Agent-Up Server.")]
    public IReadOnlyList<Workspace> ListWorkspaces() => _workspaces.List();

    [McpServerTool(Name = "get_workspace_console", Title = "Get Workspace Console")]
    [Description("Return a live console snapshot for each application in a workspace plus the recent durable console audit trail. Use when debugging hosted application output through MCP.")]
    public Task<McpToolResult> GetWorkspaceConsole(
        [Description("Registered workspace id. Optional when worktreePath is supplied.")] string? id = null,
        [Description("Absolute path to a registered workspace or worktree. Optional when id is supplied.")] string? worktreePath = null,
        [Description("Maximum live console lines per application. Defaults to 200 and is capped at 1000.")] int lineLimit = 200,
        [Description("Maximum recent console audit events. Defaults to 100 and is capped at 500.")] int auditLimit = 100,
        CancellationToken cancellationToken = default) =>
        _console.GetConsoleAsync(id, worktreePath, lineLimit, auditLimit, cancellationToken);

    [McpServerTool(Name = "get_agent_up_json_format", Title = "Get agent-up.json Format")]
    [Description("Use before creating or editing agent-up.json. Returns the current declarative agent-up.json format supported by Agent-Up.")]
    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();

    [McpServerTool(Name = "get_agent_up_context", Title = "Get Agent-Up Context")]
    [Description("Use when deciding how an AI agent should work with Agent-Up. Returns concise Agent-Up operating rules, including when to use Agent-Up MCP tools instead of curl, shell commands, or direct process starts.")]
    public string GetAgentUpContext() => _context.GetAgentUpContext();
}
