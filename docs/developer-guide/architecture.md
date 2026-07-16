---
title: Architecture
---

# Architecture

Agent-Up has seven major component areas:

- `AgentUp.Server`
- `AgentUp.Desktop`
- `AgentUp.CLI`
- `AgentUp.Installers`
- `AgentUp.InstallerApp`
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

AgentUp.Installers/
  AgentUp.Installers.csproj

AgentUp.InstallerApp/
  AgentUp.InstallerApp.csproj

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

AgentUp.InstallerApp.Tests/
  AgentUp.InstallerApp.Tests.csproj

AgentUp.Packaging.Tests/
  AgentUp.Packaging.Tests.csproj

AgentUp.PackageSmoke.Tests/
  AgentUp.PackageSmoke.Tests.csproj

AgentUp.Tests/
  AgentUp.Tests.csproj
```

The exact project list may evolve, but the ownership boundaries should remain stable.

## Code Organization

Every production .NET project uses capability-oriented vertical slices under `Features/`. Root project folders should contain only project entry/configuration files such as `Program.cs`, project files, Avalonia `App.axaml` files, and SDK/tooling-required files such as `Properties/launchSettings.json`.

Inside a feature slice, use only these type folders:

- `Models/` for persistence or internal representation.
- `Repositories/` for storage abstraction and persistence access.
- `Services/` for domain-specific behavior and orchestration inside the slice.
- `Controllers/` for routing external calls to slice services.
- `DTOs/` for external data representation contracts.
- `Providers/` for low-level actions behind domain-specific interfaces, such as HTTP clients, command runners, file-system adapters, platform adapters, and Git readers.
- `Interfaces/` for justified slice-local interfaces.
- `Factories/` for object-selection or adapter-selection factories.

Avalonia UI projects may additionally use `Views/` and `ViewModels/` inside feature slices.

Tests should stay feature-sliced, but test projects may use test-kind folders such as `Unit/`, `Headless/`, `HTTP/`, `Repository/`, `Fake/`, and `Support/` when that keeps test intent clear.

Do not add broad technical buckets such as root-level `Controllers/`, `Services/`, `Models/`, `Http/`, `Commands/`, or `Git/`. Put the code in the owning feature slice and then in the appropriate type folder.

## Interface Rule

Do not add interfaces for 1:1 concrete mappings. An interface is justified only when tests create fakes, runtime code selects among multiple adapters, or the boundary intentionally hides low-level providers such as command execution, file systems, platform APIs, storage, or network calls. Place justified interfaces in the owning slice's `Interfaces/` folder.

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

`AgentUp.Installers` owns testable installer prerequisite, component-selection, payload, adapter, progress, PATH, validation, and uninstall planning contracts. Native package assets consume or mirror those contracts.

`AgentUp.InstallerApp` owns the shared Avalonia guided installer UX. It presents the common installer flow and delegates platform-specific execution to installer adapters.

`AgentUp.Packaging` owns testable release artifact staging, package metadata generation, and orchestration of native packaging tools such as `dpkg-deb`, WiX, `pkgbuild`, and `productbuild`.

`AgentUp.PackageSmoke` owns the shared package and installed-service smoke validator used by CI smoke scripts. It validates the same abstract package, service, CLI, and uninstall properties through platform adapters while keeping shell scripts focused on selecting arguments and runner setup.

MCP clients are the primary automation clients. AI agents should use MCP directly instead of shelling through the CLI.

## Boundary Rule

All orchestration belongs in the Server. Clients may request actions and render state, but they should not decide how workspaces, ports, processes, Docker, browsers, diagnostics, or event streams are managed.
