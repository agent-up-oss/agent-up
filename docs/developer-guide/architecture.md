---
title: Architecture
---

# Architecture

Agent-Up has four major components:

- `AgentUp.Server`
- `AgentUp.Desktop`
- `AgentUp.CLI`
- MCP clients

The Server is the single source of truth. Every other component is a client.

## Solution Layout

Project directories live directly at the repository root and are included in the root solution. Agent-Up does not use `src/` or `tests/` wrapper directories.

```text
agent-up.sln

AgentUp.Server/
  AgentUp.Server.csproj

AgentUp.Desktop/
  AgentUp.Desktop.csproj

AgentUp.CLI/
  AgentUp.CLI.csproj

AgentUp.Shared/
  AgentUp.Shared.csproj

AgentUp.Server.Tests/
  AgentUp.Server.Tests.csproj

AgentUp.Desktop.Tests/
  AgentUp.Desktop.Tests.csproj

AgentUp.CLI.Tests/
  AgentUp.CLI.Tests.csproj
```

The exact project list may evolve, but the ownership boundaries should remain stable.

## Component Responsibilities

`AgentUp.Server` performs orchestration:

- Workspace registry.
- Process lifecycle.
- Port allocation.
- Docker lifecycle.
- Browser lifecycle.
- Browser profile management.
- Event recording.
- Diagnostics and health monitoring.
- Playwright generation.
- MCP server.
- REST API.

`AgentUp.Desktop` displays state and browser sessions. It does not own runtime state.

`AgentUp.CLI` is a developer convenience wrapper. It forwards commands to the Server and owns no state.

MCP clients are the primary automation clients. AI agents should use MCP directly instead of shelling through the CLI.

## Boundary Rule

All orchestration belongs in the Server. Clients may request actions and render state, but they should not decide how workspaces, ports, processes, Docker, browsers, diagnostics, or event streams are managed.
