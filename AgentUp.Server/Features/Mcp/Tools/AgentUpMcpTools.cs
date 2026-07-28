using System.ComponentModel;
using AgentUp.Server.Features.Commits.DTOs;
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
    private readonly McpCommitsController _commits;

    public AgentUpMcpTools(McpWorkspaceController workspaces, McpContextController context, McpCommitsController commits)
    {
        _workspaces = workspaces;
        _context = context;
        _commits = commits;
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

    [McpServerTool(Name = "get_agent_up_json_format", Title = "Get agent-up.json Format")]
    [Description("Use before creating or editing agent-up.json. Returns the current declarative agent-up.json format supported by Agent-Up.")]
    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();

    [McpServerTool(Name = "get_agent_up_context", Title = "Get Agent-Up Context")]
    [Description("Use when deciding how an AI agent should work with Agent-Up. Returns concise Agent-Up operating rules, including when to use Agent-Up MCP tools instead of curl, shell commands, or direct process starts.")]
    public string GetAgentUpContext() => _context.GetAgentUpContext();

    [McpServerTool(Name = "enqueue_commit", Title = "Enqueue Commit")]
    [Description("Use at the end of a task to declare a vertical-slice commit for developer review. Enqueues files and a commit message into the commit queue; the files are saved as a patch and restored to their pre-change state. The developer then runs 'agentup commits next' to stage and commit each entry. Do NOT call git add, git commit, or git stash directly.")]
    public Task<McpToolResult> EnqueueCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Short slice label identifying the logical unit of change, e.g. 'feat/auth-middleware'.")] string slice,
        [Description("Conventional commit message, e.g. 'feat: add JWT validation middleware'.")] string message,
        [Description("Repo-relative file paths to include in this commit entry. At least one required.")] IReadOnlyList<string> files,
        [Description("Optional test commands to attach to this entry, e.g. 'dotnet test'. The developer sees these as a checklist before committing.")] IReadOnlyList<string>? tests,
        CancellationToken cancellationToken)
        => _commits.EnqueueAsync(worktreePath, new EnqueueRequest(slice, message, files, tests ?? []), cancellationToken);

    [McpServerTool(Name = "get_commits_status", Title = "Get Commits Status")]
    [Description("Returns the current commit queue: queued entries with their files and messages, unassigned modified files, and any active edit session. Run this after enqueueing so the developer can see the queue before stopping.")]
    public Task<McpToolResult> GetCommitsStatus(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => _commits.GetStatusAsync(worktreePath, cancellationToken);
}
