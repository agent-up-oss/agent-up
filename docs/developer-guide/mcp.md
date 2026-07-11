---
title: MCP
---

# MCP

The Server exposes an MCP server. MCP is the primary automation interface for AI agents.

The CLI exists for human convenience. AI agents should use MCP directly.

## Resources

Example resources:

```text
workspace://current
workspace://agent-1
workspace://agent-1/browser
workspace://agent-1/logs
workspace://agent-1/events
workspace://agent-1/frontend
```

Resources expose current workspace state.

## Tools

Example tools:

- Restart Workspace.
- Stop Workspace.
- List Workspaces.
- List Applications.
- Navigate Browser.
- Click Element.
- Fill Input.
- Press Keys.
- Take Screenshot.
- Export Playwright.
- Wait For Selector.
- Wait For Text.
- Wait For Navigation.
- Retrieve Logs.
- Retrieve Diagnostics.
- Retrieve Event Stream.

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
