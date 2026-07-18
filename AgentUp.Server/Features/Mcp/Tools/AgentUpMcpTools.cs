using System.ComponentModel;
using AgentUp.Server.Features.Mcp.Controllers;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Mcp.Tools;

[McpServerToolType]
public sealed class AgentUpMcpTools
{
    private readonly McpWorkspaceController _workspaces;
    private readonly McpContextController _context;

    public AgentUpMcpTools(McpWorkspaceController workspaces, McpContextController context)
    {
        _workspaces = workspaces;
        _context = context;
    }

    [McpServerTool(Name = "start_workspace", Title = "Start Workspace")]
    [Description("Registers or updates a workspace from its agent-up.json file, then starts it through Agent-Up Server.")]
    public Task<McpToolResult> StartWorkspace(
        [Description("Absolute path to the workspace or worktree containing agent-up.json.")] string worktreePath,
        CancellationToken cancellationToken) =>
        _workspaces.StartAsync(worktreePath, cancellationToken);

    [McpServerTool(Name = "stop_workspace", Title = "Stop Workspace")]
    [Description("Stops a registered workspace by workspace id or worktree path.")]
    public Task<McpToolResult> StopWorkspace(
        [Description("Registered workspace id. Optional when worktreePath is supplied.")] string? id = null,
        [Description("Absolute path to a registered workspace or worktree. Optional when id is supplied.")] string? worktreePath = null) =>
        _workspaces.StopAsync(id, worktreePath);

    [McpServerTool(Name = "get_workspace_status", Title = "Get Workspace Status")]
    [Description("Returns one workspace status when an id or worktree path is supplied; otherwise returns all workspace statuses.")]
    public McpToolResult GetWorkspaceStatus(
        [Description("Registered workspace id. Optional.")] string? id = null,
        [Description("Absolute path to a registered workspace or worktree. Optional.")] string? worktreePath = null) =>
        _workspaces.GetStatus(id, worktreePath);

    [McpServerTool(Name = "list_workspaces", Title = "List Workspaces")]
    [Description("Lists all workspaces registered with Agent-Up Server.")]
    public IReadOnlyList<Workspace> ListWorkspaces() => _workspaces.List();

    [McpServerTool(Name = "get_agent_up_json_format", Title = "Get agent-up.json Format")]
    [Description("Returns the current declarative agent-up.json format supported by Agent-Up.")]
    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();

    [McpServerTool(Name = "get_agent_up_context", Title = "Get Agent-Up Context")]
    [Description("Returns concise Agent-Up operating rules for AI agents.")]
    public string GetAgentUpContext() => _context.GetAgentUpContext();
}
