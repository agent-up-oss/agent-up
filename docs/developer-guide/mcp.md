---
title: MCP
---

# MCP

The Server exposes an MCP server at `/mcp`. MCP is the primary automation interface for AI agents.

The CLI exists for human convenience. AI agents should use MCP directly.

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

- `start_workspace`: registers or updates a workspace from its `agent-up.json`, then starts it.
- `stop_workspace`: stops a registered workspace by workspace ID or worktree path.
- `get_workspace_status`: returns a selected workspace status or all workspace statuses.
- `list_workspaces`: lists registered workspaces.
- `get_agent_up_json_format`: returns the current declarative configuration format.
- `get_agent_up_context`: returns concise Agent-Up operating rules for AI agents.

If `start_workspace` cannot find `agent-up.json`, it instructs the agent to read `docs/user-docs/agent-up-json.md`, search for an existing `agent-up.json`, or ask the user before creating one.

Future tools will add browser inspection, interaction, diagnostics, screenshots, and Playwright export without moving orchestration out of the Server.

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
