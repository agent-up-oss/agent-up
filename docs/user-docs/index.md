---
title: Agent-Up
slug: /
---

# Agent-Up

Agent-Up is a local runtime control plane for parallel AI-assisted software development.

It manages the running development environment around applications: workspaces, Git worktrees, application processes, port allocation, Docker services, isolated browser profiles, logs, diagnostics, event history, and automation surfaces.

Agent-Up is not an application framework, deployment tool, IDE, Docker replacement, Git replacement, or production orchestrator.

> **Development Preview:** Agent-Up is under active development. CI produces preliminary platform artifacts, but APIs, configuration, installer behavior, and workflows may change without notice.

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

## Getting Started

See [Current Limitations](./limitations.md) for the current implementation status of each major area.

### 1. Start the server

Installed Desktop artifacts run the Server as the local `agent-up-server` service.

From the repository root:

```bash
dotnet run --project AgentUp.Server
```

The server starts on `http://localhost:5000` in the current development launch profile.

### 2. Add an `agent-up.json` to your project

Create `agent-up.json` in the root of the repository you want to manage:

```json
{
  "name": "My App",
  "applications": [
    {
      "name": "Frontend",
      "command": "npm run dev",
      "path": "."
    }
  ]
}
```

### 3. Push the workspace definition

From your project directory, run the CLI with `dotnet run`:

```bash
dotnet run --project /path/to/AgentUp.CLI -- start --server http://localhost:5000
```

Or set the server URL once as an environment variable and omit `--server` on every call:

```bash
export AGENTUP_SERVER_URL=http://localhost:5000
dotnet run --project /path/to/AgentUp.CLI -- start
```

This reads `agent-up.json`, captures the current git branch and commit, and pushes the workspace and application definitions to the server. The server then exposes them at `GET /api/workspaces/{id}/applications` for the desktop app to consume.

## Documentation Map

- [Workspace](./workspace.md) defines the workspace model.
- [Setup](./setup.md) describes the current source-first development workflow.
- [Releases](./releases.md) describes packaged artifacts and MinIO/S3 release upload.
- [Current Limitations](./limitations.md) explains what is implemented, experimental, in progress, and planned.
- [Browser](./browser.md) covers shared browser sessions and automation.
- [Browser Profiles](./browser-profiles.md) explains per-workspace browser isolation.
- [CLI](./cli.md) covers the human-friendly command wrapper.
- [Configuration](./configuration.md) and [agent-up.json](./agent-up-json.md) describe declarative application setup.
- [Roadmap](./roadmap.md) captures the long-term direction.

Implementation details live in the [Developer Guide](/developer-guide/).
