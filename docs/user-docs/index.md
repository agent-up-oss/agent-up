---
title: Agent-Up
slug: /
---

# Agent-Up

Agent-Up is a cross-platform development workspace manager for AI-assisted software development.

It manages development workspaces. It is not an application framework, deployment tool, IDE, or application orchestrator.

Every AI agent works in its own Git worktree with its own branch, runtime environment, browser profile, infrastructure, application state, diagnostics, and event history. Agent-Up makes switching between these isolated workspaces effortless while letting developers and AI agents interact with the same running applications.

## What Agent-Up Solves

Modern AI-assisted development creates multiple parallel runtimes. Existing tooling usually assumes one developer running one application instance, which leads to:

- Constantly starting and stopping services.
- Docker infrastructure collisions.
- Browser tab sprawl.
- Duplicated authentication flows.
- Inconsistent runtime state.
- Manual process management.
- Difficult validation of AI-generated changes.

Agent-Up solves these problems without requiring changes to application source code.

## System Shape

The Server is the single source of truth. Desktop, CLI, and MCP clients are all thin clients over server-owned runtime state.

```text
                +----------------------+
                |   AgentUp.Server     |
                |----------------------|
                | Workspace Manager    |
                | Process Manager      |
                | Browser Manager      |
                | Port Manager         |
                | Event Recorder       |
                | Diagnostics          |
                | Playwright Generator |
                | MCP Server           |
                +----------+-----------+
                           |
        +------------------+-------------------+
        |                  |                   |
+---------------+   +---------------+   +------------------+
| Avalonia UI   |   | AgentUp CLI   |   | MCP Clients      |
| Human UI      |   | Thin Wrapper  |   | ChatGPT          |
|               |   |               |   | Claude / Codex   |
+---------------+   +---------------+   +------------------+
```

## Documentation Map

- [Workspace](./workspace.md) defines the workspace model.
- [Browser](./browser.md) covers shared browser sessions and automation.
- [Browser Profiles](./browser-profiles.md) explains per-workspace browser isolation.
- [CLI](./cli.md) covers the human-friendly command wrapper.
- [Configuration](./configuration.md) and [agent-up.json](./agent-up-json.md) describe declarative application setup.
- [Roadmap](./roadmap.md) captures the long-term direction.

Implementation details live in the [Developer Guide](/developer-guide/).
