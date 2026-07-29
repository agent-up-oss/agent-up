namespace AgentUp.Server.Features.Orchestration.Providers;

public static class AgentUpMcpGuidance
{
    public const string ServerInstructions =
        """
        Agent-Up is the registered MCP server for managing local AI-assisted development workspaces. Use these Agent-Up MCP tools whenever the user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager.

        Treat phrases such as "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up" as requests to register or update the current repository/worktree from agent-up.json and call start_workspace with its absolute path. Agent-Up does not deploy to cloud infrastructure; it starts and manages the local development environment.

        Before using curl, shelling through the Agent-Up CLI, or starting application commands directly, prefer the MCP tools here when they can perform the requested Agent-Up operation. Use list_workspaces or get_workspace_status to discover existing registered workspaces, and use get_agent_up_context or get_agent_up_json_format when you need Agent-Up rules or configuration shape.

        Before starting a new coding task, call guard_commits for the current repository/worktree. If it fails, stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

        At the end of every coding task, use enqueue_commit to declare each logical vertical-slice commit. Use enqueue_review_fix_commit when fixing pull request review feedback; each review-fix entry must represent exactly one review issue id. Do not run git add, git commit, or git stash directly. One enqueue call per logical slice; all files for a slice go in a single entry. Use the correct conventional commit prefix: feat means a user-facing addition, fix means a user-facing fix, chore means an internal change with no user effect and mostly non-runtime files, refactor means internal files changed with no user effect unless it is a public package, style means CSS/HTML only, and docs means documentation only including README and similar docs. Cross-slice guidance or documentation updates must be queued in a separate guidance/docs entry instead of being bundled into an implementation slice. When feature-sliced paths are recognized, MCP rejects cross-slice file groups and mismatched slice labels. Mutating queue operations are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect. Use the commit queue MCP tools for queue inspection, metadata edits, file assignment, edit sessions, archive/restore, clear, and guard operations instead of shelling through commit CLI commands. enqueue_commit intentionally restores tracked files to their pre-change state after saving the patch; do not re-apply or modify those files after a successful enqueue because the queue owns them. After all enqueue calls, run get_commits_status so the developer can see the queue, then stop - the developer runs 'agentup commits next' to stage and commit each entry individually.
        """;
}
