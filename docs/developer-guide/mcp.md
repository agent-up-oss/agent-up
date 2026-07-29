---
title: MCP
---

# MCP

The Server exposes an MCP server at `/mcp`. MCP is the primary automation interface for AI agents.

The CLI exists for human convenience. AI agents should use MCP directly.

The Server sends MCP initialization instructions that tell clients to use Agent-Up tools whenever a user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager. User wording such as "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up" means the agent should call `start_workspace` with the absolute repository/worktree path. Agent-Up starts and manages local development environments; it does not deploy to cloud infrastructure.

The Server exposes Streamable HTTP at `/mcp` and legacy SSE compatibility at `/mcp/sse` plus `/mcp/message`. MCP is a protocol surface, not a feature slice: tools and resources are thin controller-layer adapters owned by the feature slice whose capability they expose. Cross-capability workspace and context tools live in the `Orchestration` slice.

## Resources

Initial resources:

```text
agent-up://context
agent-up://agent-up-json
agent-up://workspaces
agent-up://workspaces/{id}
```

Resources expose Agent-Up context, the declarative `agent-up.json` format, and current workspace state.

## Tools

Initial tools:

- `start_workspace`: registers or updates a workspace from its `agent-up.json`, then starts it. Use it for requests to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up.
- `stop_workspace`: stops a registered workspace by workspace ID or worktree path.
- `get_workspace_status`: returns a selected workspace status or all workspace statuses.
- `list_workspaces`: lists registered workspaces.
- `get_agent_up_json_format`: returns the current declarative configuration format.
- `get_agent_up_context`: returns concise Agent-Up operating rules for AI agents.
- `enqueue_commit`: saves a vertical-slice patch in the commit queue and restores the tracked files to their pre-change state for `agentup commits next`.
- `enqueue_review_fix_commit`: saves one review issue violation fix with a required `reviewIssueId`; do not combine multiple review issues in one entry.
- `get_commits_status`: returns queued entries, unassigned modified files, any active commit edit session, and active Git operation state.
- `guard_commits`: blocks new work while queued entries, active edit sessions, staged changes, unassigned modified files, or active Git merge/rebase/cherry-pick/revert/bisect operations exist.
- `get_commit_changes`: returns working-tree files with queue assignment information.
- `inspect_commit`: returns one queued entry, optionally including the saved patch.
- `update_commit_message`, `update_commit_tests`, `add_commit_files`, `remove_commit_files`: update queued entry metadata and file assignment.
- `remove_commit`, `restore_commit`, `clear_commits`: archive, restore, or clear queued entries.
- `begin_commit_edit`, `save_commit_edit`, `abort_commit_edit`: safely edit an existing queued patch.

If `start_workspace` cannot find `agent-up.json`, it instructs the agent to read `docs/user-docs/agent-up-json.md`, search for an existing `agent-up.json`, or ask the user before creating one.

`commits next` remains CLI-only because staging and popping a queued entry is developer-owned review work. Agents stop after `get_commits_status`.

When the Server recognizes feature-sliced paths under `Features/<Slice>/`, MCP enqueue operations reject entries that span multiple slices or whose slice label does not match the recognized slice. Repositories without recognized vertical-slice paths fall back to unscoped queue entries.

Cross-slice guidance or documentation updates should be queued in a separate guidance/docs entry instead of being bundled into an implementation slice.

Future tools will add browser inspection, interaction, diagnostics, screenshots, and Playwright export without moving orchestration out of the Server.

## Commit Queue Reminders

MCP has no server-side lifecycle callback for when an agent finishes a turn. The Server cannot push a post-job reminder; it can only respond to tool calls.

Agents should call `guard_commits` before starting a new coding task. If it fails, they should stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

Claude Code installations can surface commit queue reminders with a client-side `Stop` hook. Configure the hook to run `agentup commits guard` and print a reminder when tracked files are dirty but not assigned to a queued entry:

```json
"hooks": {
  "Stop": [
    {
      "matcher": "",
      "hooks": [
        {
          "type": "command",
          "command": "agentup commits guard 2>/dev/null | grep -q 'modified file(s) are not assigned' && echo '[agent-up] Unqueued changes detected - run: agentup commits enqueue' || true"
        }
      ]
    }
  ]
}
```

`enqueue_commit` intentionally restores tracked files after saving the patch. Its success message must tell agents not to re-apply or modify those files because the queue owns them until the developer runs `agentup commits next`.

Mutating commit queue operations are blocked while Git reports an active merge, rebase, cherry-pick, revert, or bisect. Integrations should surface the returned operation state and ask the developer to finish or abort the Git operation first.

## Automation Flow

AI agents interact with applications through MCP:

```text
inspect_page
↓
click
↓
fill
↓
press
↓
wait
↓
screenshot
```

The Server executes browser operations.
