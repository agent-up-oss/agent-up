---
title: Architecture
---

# Architecture

Agent-Up has six major component areas:

- `AgentUp.Server`
- `AgentUp.Desktop`
- `AgentUp.CLI`
- `AgentUp.Installers`
- `AgentUp.Packaging`
- `AgentUp.PackageSmoke`
- MCP clients

The Server is the single source of truth for runtime orchestration. Desktop, CLI, MCP clients, and future integrations are clients of the Server. Installer code owns machine installation planning and validation only; it does not own runtime state.

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

AgentUp.Installers/
  AgentUp.Installers.csproj

AgentUp.Packaging/
  AgentUp.Packaging.csproj

AgentUp.PackageSmoke/
  AgentUp.PackageSmoke.csproj

AgentUp.Server.Tests/
  AgentUp.Server.Tests.csproj

AgentUp.Desktop.Tests/
  AgentUp.Desktop.Tests.csproj

AgentUp.CLI.Tests/
  AgentUp.CLI.Tests.csproj

AgentUp.Installers.Tests/
  AgentUp.Installers.Tests.csproj

AgentUp.Packaging.Tests/
  AgentUp.Packaging.Tests.csproj

AgentUp.PackageSmoke.Tests/
  AgentUp.PackageSmoke.Tests.csproj
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

`AgentUp.Installers` owns testable installer prerequisite, component-selection, PATH, validation, and uninstall planning contracts. Native package assets consume or mirror those contracts.

`AgentUp.Packaging` owns testable release artifact staging, package metadata generation, and orchestration of native packaging tools such as `dpkg-deb`, WiX, `pkgbuild`, and `productbuild`.

`AgentUp.PackageSmoke` owns the shared package and installed-service smoke validator used by CI smoke scripts. It validates the same abstract package, service, CLI, and uninstall properties through platform adapters while keeping shell scripts focused on selecting arguments and runner setup.

MCP clients are the primary automation clients. AI agents should use MCP directly instead of shelling through the CLI.

## Boundary Rule

All orchestration belongs in the Server. Clients may request actions and render state, but they should not decide how workspaces, ports, processes, Docker, browsers, diagnostics, or event streams are managed.
