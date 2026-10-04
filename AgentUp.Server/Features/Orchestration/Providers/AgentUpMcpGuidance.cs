using AgentUp.Server.Features.Orchestration.DTOs;

namespace AgentUp.Server.Features.Orchestration.Providers;

/// <summary>
/// The instructions an MCP client reads on initialize. They used to say "local" and tell every
/// caller to start a workspace by absolute path, which is only true when the client and the
/// Server share a filesystem. The addressing rule is the part that changes per caller; the
/// validation feedback loop and the commit-queue discipline are the same wherever it connects.
/// </summary>
public static class AgentUpMcpGuidance
{
    public static string ServerInstructions { get; } = Build(McpInstructionContext.SharedFilesystem);

    public static string ForSharedFilesystem() => Build(McpInstructionContext.SharedFilesystem);

    public static string ForRemote() => Build(McpInstructionContext.Remote);

    public static string ForPinned(string workspaceId) => Build(McpInstructionContext.Pinned(workspaceId));

    public static string Build(McpInstructionContext context) => string.Join(
        "\n\n",
        Identity,
        Addressing(context),
        ToolPreference(context),
        ValidationLoop,
        CommitGuard(context),
        CommitDiscipline);

    /// <summary>
    /// Endpoint servers describe their own slice, so they replace the body rather than extend it.
    /// They still carry the addressing rule: a commits-only client has the same filesystem
    /// question to answer as an orchestration client, and must not be told a different answer.
    /// </summary>
    public static string ForEndpoint(string sliceInstructions, McpInstructionContext context)
        => $"{sliceInstructions}\n\n{Addressing(context)}";

    private const string Identity =
        "Agent-Up is the registered MCP server for managing AI-assisted development workspaces. Use these Agent-Up MCP tools whenever the user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager.";

    private const string TriggerPhrases =
        "\"deploy my app with Agent-Up\", \"run my app with Agent-Up\", \"start this workspace\", \"bring up the app\", \"serve this repo\", or \"open the app in Agent-Up\"";

    private const string NotCloudDeployment =
        "Agent-Up does not deploy to cloud infrastructure; it starts and manages the development environment this Server hosts.";

    public static string Addressing(McpInstructionContext context) => context.Audience switch
    {
        McpInstructionAudience.Pinned => PinnedAddressing(context.WorkspaceId),
        McpInstructionAudience.Remote => RemoteAddressing,
        _ => SharedFilesystemAddressing
    };

    private const string SharedFilesystemAddressing =
        $"Treat phrases such as {TriggerPhrases} as requests to register or update the current repository/worktree from agent-up.json and call start_workspace with its absolute path immediately. This Server runs on the host you are on, so a path you can see is a path it can open. Do not call list_workspaces or get_workspace_status before start_workspace for a known current repository/worktree. {NotCloudDeployment}";

    private const string RemoteAddressing =
        $"Treat phrases such as {TriggerPhrases} as requests to start a workspace this Server already holds. You do not share a filesystem with this Server, so a path from your machine names nothing it can open: call list_workspaces, choose the workspace the user means, and pass its id to start_workspace and to every later tool. Any path a tool returns, including a managed proposal-queue worktree, is a location on the Server's host and not one you can open yourself. {NotCloudDeployment}";

    private static string PinnedAddressing(string? workspaceId)
        => $"This session is pinned to workspace {Named(workspaceId)}, and every tool here already targets it. There is no workspace to choose: do not call list_workspaces to find one, do not name another workspace id, and do not pass a path from your own filesystem, which this Server may not share. Treat phrases such as {TriggerPhrases} as requests to call start_workspace for this pinned workspace. Any path a tool returns is a location on the Server's host. {NotCloudDeployment}";

    private static string Named(string? workspaceId)
        => string.IsNullOrWhiteSpace(workspaceId) ? "a single workspace" : workspaceId;

    private static string ToolPreference(McpInstructionContext context) => context.Audience switch
    {
        McpInstructionAudience.Pinned =>
            "Prefer the MCP tools here over curl, the Agent-Up CLI, or starting application commands yourself. Use get_workspace_status to inspect the pinned workspace or to answer an explicit status question; list_workspaces returns only that workspace. Use get_agent_up_context or get_agent_up_json_format when you need Agent-Up rules or configuration shape.",
        McpInstructionAudience.Remote =>
            "Prefer the MCP tools here over curl or starting application commands yourself. The Agent-Up CLI is not a substitute from here: it acts on the Server's host, not on yours. Use get_workspace_status to inspect an already-running workspace or to answer an explicit status/list question. Use get_agent_up_context or get_agent_up_json_format when you need Agent-Up rules or configuration shape.",
        _ =>
            "Before using curl, shelling through the Agent-Up CLI, or starting application commands directly, prefer the MCP tools here when they can perform the requested Agent-Up operation. Use list_workspaces or get_workspace_status only when you need to choose among existing workspaces, inspect an already-running workspace, or answer an explicit status/list question. Use get_agent_up_context or get_agent_up_json_format when you need Agent-Up rules or configuration shape."
    };

    private const string ValidationLoop =
        "Agent-Up validation is a feedback loop. After start_workspace, use the returned workspace id and allocated ports directly for browser MCP navigation; those ports are bound on the Server's host and the browser MCP tools reach them from there. If browser navigation, inspection, waiting, screenshots, or interaction fails or times out, inspect the workspace console immediately through get_workspace_console on Orchestration MCP. If that tool is unavailable, query Audit MCP for recent application console events with kind=application and source=process for the workspace. Treat console output as the first diagnostic source; it often shows missing dependencies, failed commands, port binding errors, Docker startup failures, or build/runtime crashes. Fix the concrete console issue, then call start_workspace again and repeat the browser validation.";

    private static string CommitGuard(McpInstructionContext context) => context.Audience switch
    {
        McpInstructionAudience.Pinned =>
            "Before starting a new coding task, call guard_commits for this pinned workspace. When it succeeds with continueWorktreePath, that path names the managed proposal worktree on the Server's host; continue dependent work there through these tools. If it fails, stop unless the user explicitly asked to inspect, debug, or continue existing changes.",
        McpInstructionAudience.Remote =>
            "Before starting a new coding task, call guard_commits for the workspace you are about to change. When it succeeds with continueWorktreePath, that path names the managed proposal worktree on the Server's host; continue dependent work there through these tools rather than through a checkout of your own. If it fails, stop unless the user explicitly asked to inspect, debug, or continue existing changes.",
        _ =>
            "Before starting a new coding task, call guard_commits for the current repository/worktree. When it succeeds with continueWorktreePath, switch the ACP task to that managed proposal worktree and build on the queue tip. If it fails, stop unless the user explicitly asked to inspect, debug, or continue existing changes."
    };

    private const string CommitDiscipline =
        "At the end of every coding task, use enqueue_commit to declare each logical vertical-slice commit. Use enqueue_review_fix_commit when fixing pull request review feedback; each review-fix entry must represent exactly one review issue id. Do not run git add, git commit, or git stash directly. One enqueue call per logical slice; all files for a slice go in a single entry. Scope every conventional commit message to the queued slice, for example fix(Commits): validate queue metadata. Use the correct conventional commit prefix: feat means a user-facing addition, fix means a user-facing fix, test means test-only or smoke-validation changes, chore means maintenance/packaging/CI/tooling with no customer runtime effect, refactor means internal source changes with no behavior change, style means CSS/HTML only, and docs means documentation only including README and similar docs. If agent-up.json contains prompts.commitPolicy, follow that repository-specific policy when choosing prefixes and grouping commits. Cross-slice guidance or documentation updates must be queued in a separate guidance/docs entry instead of being bundled into an implementation slice. When feature-sliced paths are recognized, MCP rejects cross-slice file groups and mismatched slice labels. Mutating queue operations are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect. Use the commit queue MCP tools for queue inspection, metadata edits, file assignment, edit sessions, archive/restore, clear, and guard operations instead of shelling through commit CLI commands. For a Git proposal queue, enqueue_commit returns queueWorktreePath as structured data; continue later work there. Legacy enqueue restores tracked files and remains developer-staged with agentup commits next. After all enqueue calls, run get_commits_status so the developer can see the queue.";
}
