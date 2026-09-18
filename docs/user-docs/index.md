---
title: Agent-Up
slug: /
---

# Agent-Up

Agent-Up is a local runtime control plane for parallel AI-assisted software development.

It manages the running development environment around applications: workspaces, Git worktrees, application processes, port allocation, Docker services, isolated browser profiles, logs, diagnostics, event history, and automation surfaces.

Agent-Up is not an application framework, deployment tool, IDE, Docker replacement, Git replacement, or production orchestrator.

> **Development Preview:** Agent-Up is under active development. Packaged artifacts, APIs, configuration, installer behavior, and workflows may change without notice.

Every AI agent can work in its own Git worktree with its own branch, runtime environment, browser profile, infrastructure, application state, diagnostics, and event history. Agent-Up is being built to make switching between these isolated workspaces practical while letting developers and AI agents interact with the same running applications.

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

The Server is the single source of truth. Desktop, Mobile, CLI, and MCP clients are all thin clients over server-owned runtime state.

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
                | MCP servers          |
                +----------+-----------+
                           |
        +------------------+-------------------+------------------+
        |                  |                   |                  |
+---------------+   +---------------+   +---------------+   +------------------+
| Desktop       |   | Mobile        |   | CLI           |   | MCP clients      |
| Avalonia UI   |   | Expo / PWA    |   | agent-up      |   | named /mcp/*     |
+---------------+   +---------------+   +---------------+   +------------------+
```

## Getting Started

See [Current Limitations](./limitations.md) for the current implementation status of each major area.

### 1. Start the server

Installed Desktop artifacts run the Server as the local `agent-up-server` service.

From the repository root:

```bash
dotnet run --project AgentUp.Server
```

The server starts on `http://localhost:5001` in the current development launch profile.

### 2. Add an `agent-up.json` to your project

Create `agent-up.json` in the root of the repository you want to manage:

```json
{
  "name": "My App",
  "applications": [
    {
      "name": "Frontend",
      "command": "npm run dev",
      "path": ".",
      "ports": [
        { "variable": "PORT", "defaultPort": 3000 }
      ]
    }
  ]
}
```

### 3. Push the workspace definition

From your project directory, run the CLI with `dotnet run`:

```bash
dotnet run --project /path/to/AgentUp.CLI -- start --server http://localhost:5001
```

Or set the server URL once as an environment variable and omit `--server` on every call:

```bash
export AGENTUP_SERVER_URL=http://localhost:5001
dotnet run --project /path/to/AgentUp.CLI -- start
```

This reads `agent-up.json`, captures the current git branch and commit, and pushes the workspace and application definitions to the server. The server then exposes them at `GET /api/workspaces/{id}/applications` for the desktop app to consume.

## Documentation Map

- [Downloads](./downloads.md) lists current development-preview packages.
- [Setup](./setup.md) describes packaged and source-first development workflows.
- [Workspace](./workspace.md) defines the workspace model.
- [Mobile](./mobile.md) covers the Expo client for Android, iOS, and the PWA.
- [Git changes](./git-changes.md) is the human working-tree review surface.
- [CLI](./cli.md) covers the `agent-up` command wrapper.
- [Browser](./browser.md) covers Desktop, Mobile, and Server browser surfaces.
- [Browser Profiles](./browser-profiles.md) explains per-workspace headless isolation.
- [Configuration](./configuration.md) and [agent-up.json](./agent-up-json.md) describe declarative application setup.
- [Releases](./releases.md) describes packaged artifacts and update behavior.
- [Current Limitations](./limitations.md) explains what is implemented, Preview, Experimental, and Planned.
- [Roadmap](./roadmap.md) captures the long-term direction.

Implementation details live in the [Developer Guide](/developer-guide/).
