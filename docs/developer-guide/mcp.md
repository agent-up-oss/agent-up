---
title: MCP
---

# MCP

The Server exposes an MCP server at `/mcp`. MCP is the primary automation interface for AI agents.

The CLI exists for human convenience. AI agents should use MCP directly.

The Server sends MCP initialization instructions that tell clients to use Agent-Up tools whenever a user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager. User wording such as "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up" means the agent should call `start_workspace` with the absolute repository/worktree path. Agent-Up starts and manages local development environments; it does not deploy to cloud infrastructure.

The Server exposes Streamable HTTP at `/mcp` and legacy SSE compatibility at `/mcp/sse` plus `/mcp/message`. Tools and resources are thin adapters over shared Server-owned MCP domain services so future Server model changes do not fork agent behavior across transports.

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
- `get_commits_status`: returns queued entries, unassigned modified files, and any active commit edit session.

If `start_workspace` cannot find `agent-up.json`, it instructs the agent to read `docs/user-docs/agent-up-json.md`, search for an existing `agent-up.json`, or ask the user before creating one.

Future tools will add browser inspection, interaction, diagnostics, screenshots, and Playwright export without moving orchestration out of the Server.

## Commit Queue Reminders

MCP has no server-side lifecycle callback for when an agent finishes a turn. The Server cannot push a post-job reminder; it can only respond to tool calls.

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
