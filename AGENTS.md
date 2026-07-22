# General

Agent-Up is a .NET solution for managing AI-assisted development workspaces.

It is not an application framework, deployment tool, IDE, or application orchestrator. Agent-Up owns the running development environment around applications: worktrees, processes, ports, Docker lifecycle, browser profiles, diagnostics, event history, and automation surfaces.

The authoritative product and implementation documentation lives in:

- User docs: `docs/user-docs/`
- Developer guide: `docs/developer-guide/`

User docs are for people using Agent-Up. They should describe product concepts, setup, downloads, configuration, and operational troubleshooting in user-facing terms. Do not put implementation details, project ownership, test architecture, CI topology, release pipeline internals, package smoke mechanics, private environment variables, or native packaging tool orchestration in user docs unless the user must perform that action directly.

Developer guide pages are for contributors and maintainers. Put architecture decisions, ownership rules, project layout, CI/release workflow, test strategy, package validation, installer internals, native packaging tool details, and implementation contracts there.

When editing docs, keep diffs reviewable. Prefer targeted edits to the specific sentence, list item, or small paragraph that changed. Do not rewrite whole paragraphs or broad blocks just to add a narrow detail. If a section regularly needs small updates, split it into shorter focused paragraphs or bullets so future changes can touch only the relevant part.

# Definition Synchronization

`AGENTS.md` and the relevant docs pages are definition sources for the project.

Any change that alters behavior, architecture, ownership, project layout, workflows, configuration shape, runtime contracts, testing rules, or implementation guidance must update the matching definition source in the same change.

If code or docs are derived from the current state of `AGENTS.md`, update `AGENTS.md` when that definition changes. If `AGENTS.md` points to a docs page for the detailed definition, update that docs page when the source definition changes.

Do not leave implementation, `AGENTS.md`, and docs disagreeing. If the requested change intentionally supersedes existing guidance, update the guidance first or as part of the same commit.

# Architecture

Agent-Up is organized around one rule:

**AgentUp.Server is the single source of truth.**

Desktop, CLI, MCP clients, and future integrations are clients of the Server. They may display state and request actions, but they must not own runtime state or duplicate orchestration logic.

Packaged Desktop installations include the Server and run it as the local `agent-up-server` service. This is an installation/lifecycle concern only: Desktop remains a client and the Server remains the single source of truth.

Expected solution shape:

```text
agent-up.sln

AgentUp.Server/
  AgentUp.Server.csproj

AgentUp.Capabilities.Abstractions/
  AgentUp.Capabilities.Abstractions.csproj

AgentUp.Capabilities.Common/
  AgentUp.Capabilities.Common.csproj

AgentUp.Capabilities.Dotnet/
  AgentUp.Capabilities.Dotnet.csproj

AgentUp.Capabilities.Docker/
  AgentUp.Capabilities.Docker.csproj

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

AgentUp.Capabilities.Abstractions.Tests/
  AgentUp.Capabilities.Abstractions.Tests.csproj

AgentUp.Capabilities.Common.Tests/
  AgentUp.Capabilities.Common.Tests.csproj

AgentUp.Capabilities.Dotnet.Tests/
  AgentUp.Capabilities.Dotnet.Tests.csproj

AgentUp.Capabilities.Docker.Tests/
  AgentUp.Capabilities.Docker.Tests.csproj

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

AgentUp.Architecture.Tests/
  AgentUp.Architecture.Tests.csproj

AgentUp.Tests/
  AgentUp.Tests.csproj
```

Project directories live directly at the repository root and are included in the root solution. Do not introduce `src/` or `tests/` wrapper directories unless the repository is intentionally reorganized everywhere.

The exact project list may evolve, but ownership must not drift:

| Area | Owns |
|---|---|
| `AgentUp.Server` | Workspace registry, process lifecycle, ports, Docker, browser lifecycle, diagnostics, event recording, MCP, REST API |
| `AgentUp.Capabilities.Abstractions` | Stable capability adapter interfaces, manifest DTOs, installed-version inventory contracts, validation results, and launch plans |
| `AgentUp.Capabilities.Common` | Shared capability catalog parsing, checksum validation, Agent-Up tool-cache layout, and install planning used by first-party and future external capabilities |
| `AgentUp.Capabilities.Dotnet` | First-party .NET ecosystem adapter, SDK discovery, version reconciliation, and `dotnet` launch planning |
| `AgentUp.Capabilities.Docker` | First-party Docker ecosystem adapter, Docker discovery, validation, and Docker launch planning |
| `AgentUp.Desktop` | Avalonia UI, workspace display, logs, diagnostics, embedded/shared browser views |
| `AgentUp.CLI` | Thin human-friendly command wrapper over Server capabilities |
| `AgentUp.Installers` | Testable installer prerequisite, component selection, PATH, validation, and uninstall planning contracts |
| `AgentUp.InstallerApp` | Shared Avalonia installer dashboard over platform installer adapters and capability catalog state |
| `AgentUp.Packaging` | Testable release artifact staging, package metadata generation, and native packaging tool orchestration |
| `AgentUp.PackageSmoke` | Testable package and installed-service smoke validation adapters used by CI smoke scripts |
| MCP clients | Automation interface; no local orchestration |

Read the full architecture guide before making structural changes: `docs/developer-guide/architecture.md`.

# New Architecture

The project should be implemented as capability-oriented slices inside each owning project rather than broad technical buckets.

Prefer this:

```text
AgentUp.Server/
  Features/
    Workspaces/
      Controllers/
      DTOs/
      Factories/
      Interfaces/
      Models/
      Providers/
      Repositories/
      Services/
    Processes/
      Interfaces/
      Providers/
      Services/
    Applications/
      Controllers/
      DTOs/
    Browser/
      Services/
      Profiles/
      Automation/
    Ports/
      Interfaces/
      Models/
      Providers/
      Services/
    Mcp/
      Tools/
      Resources/

AgentUp.Desktop/
  Features/
    Workspaces/       (sidebar navigation: workspace list, loading, error, collapse)
      DTOs/
      Factories/
      Providers/
      Repositories/
      ViewModels/
      Views/
    Applications/     (application tab bar: list and selection per workspace)
      DTOs/
      ViewModels/
    Console/          (console output/logs for the selected application)
      Providers/
      ViewModels/
    Ports/            (port sub-tabs: HTTP browser view, TCP info, probe status)
      DTOs/
      ViewModels/

AgentUp.CLI/
  Features/
    Workspaces/       (human CLI commands over Server workspace capabilities)
      Controllers/
      DTOs/
      Factories/
      Interfaces/
      Models/
      Providers/
      Services/

AgentUp.Installers/
  Features/
    Installation/     (guided install flow, component selection, payloads, PATH, validation, uninstall planning)
      DTOs/
      Factories/
      Interfaces/
      Models/
      Providers/
      Services/
    PrerequisiteChecks/ (Docker status and minimum-version checks)
      Interfaces/
      Models/
      Providers/
      Services/
    UbuntuInstallation/ (systemd service, CLI, desktop launcher install adapter contracts)
      DTOs/
      Interfaces/
      Models/
      Providers/
    MacOsInstallation/ (launchd service, CLI, app bundle install adapter contracts)
      DTOs/
      Interfaces/
      Models/
      Providers/
      Services/
    WindowsInstallation/ (Windows Service, PATH, Start Menu, WiX install adapter contracts)
      DTOs/
      Interfaces/
      Models/
      Providers/
      Services/

AgentUp.Packaging/
  Features/
    ReleaseArtifacts/ (artifact requests, repository paths, command execution)
      Controllers/
      DTOs/
      Interfaces/
      Models/
      Providers/
      Services/
    UbuntuPackages/   (Debian package layout, metadata, staging, dpkg orchestration)
      Controllers/
      Interfaces/
      Models/
      Providers/
      Services/
    WindowsPackages/  (WiX/Burn orchestration)
      Controllers/
      Interfaces/
      Models/
      Providers/
      Services/
    MacOsPackages/    (pkg/signing/notarization orchestration)
      Controllers/
      Interfaces/
      Models/
      Providers/
      Services/
    NixOs/            (flake package-set orchestration when implemented)
  Shared/
    Interfaces/       (cross-slice low-level abstractions such as command and file-system access)
    Providers/
    Factories/        (project composition root for long-lived service/provider/controller instances)

AgentUp.PackageSmoke/
  Features/
    SmokeRuns/        (package-smoke command parsing, work directory preparation, and validation routing)
      Controllers/
      DTOs/
      Factories/
      Interfaces/
      Providers/
      Services/
    PackageValidation/
      DTOs/
      Factories/
      Interfaces/
      Providers/
      Services/
    InstalledServiceValidation/
      DTOs/
      Factories/
      Interfaces/
      Models/
      Providers/
      Services/
    InstallerFlowValidation/
      Services/
    RuntimeSecurity/
      Interfaces/
      Providers/
      Services/
```

Avoid this as the primary organizing model:

```text
AgentUp.Server/
  Controllers/
  Services/
  Repositories/
  Models/
```

The same structure applies to tests:

```text
AgentUp.Server.Tests/
  Features/
    Workspaces/
      HTTP/
      Unit/
      Repository/
    Browser/
      Unit/
      Automation/
    Mcp/
      Tools/
      Resources/
```

Prefer working only in the slice directly involved in the task.

Feature slice names should have product, customer, operator, or maintainer meaning. Avoid creating top-level slices for tiny technical mechanisms such as payload parsing, PATH editing, execution helpers, or validation records when they are only part of a larger capability; keep those as type-folder contents inside the meaningful owning slice.

## Migrations And Persistence

If persistent storage is introduced, migrations stay together in the owning infrastructure/migration location for the project.

Feature separation happens at repository/service boundaries. Do not scatter migration files by feature unless the project explicitly adopts that convention later.

## Inter-Slice Communication

A slice owns its writes.

Project entrypoints such as `Program.cs`, host routes, CLI commands, MCP tools, and UI event handlers should call into a slice through `Controllers/`, either directly or through the project composition root that exposes those controllers. Controllers receive dependencies through constructors; they must not create services or providers. Keep controllers thin: they map external calls and DTO arguments to injected services.

Services own domain lifecycle and orchestration behind controllers. Services may call same-slice repositories, providers, factories, and models, but they must stay domain-specific. Services must not contain low-level parsing, command construction, filesystem/archive operations, native tool invocation, environment lookup, HTTP/network mechanics, process execution, platform API calls, XML/manifest serialization mechanics, or string-scanning helpers for external tool output. Put that behavior behind same-slice `Providers/` with names that describe the user/operator capability where practical, such as `PackageCommandParser`, `DpkgDebPackageTool`, `WindowsWixPackagingTool`, `MacOsPackageArchiveProvider`, or `DockerPrerequisiteProvider`.

Use `Models/` for data definitions and pure internal representations that stay inside the slice, including generated manifest/script/XML text when the code is defining package or installer data rather than performing I/O. Use `DTOs/` only for data crossing external or controller boundaries.

Provider interfaces are justified when they hide low-level providers from services, are faked by tests, or select runtime adapters. A service depending on `IUbuntuPackageTool` is acceptable; a service building `new CommandSpec("dpkg-deb", ...)` is not. A controller or service parsing raw `string[] args` is not acceptable; use a parser provider that returns a DTO/result.

Slices must not reach directly into another slice's internal `Services/`, `Models/`, `Providers/`, `Interfaces/`, `Repositories/`, or `Factories/`. Cross-slice calls go through the target slice's `Controllers/` boundary and exchange IDs or `DTOs/`. If a low-level abstraction is genuinely shared by multiple slices, place it in a project-level shared folder instead of hiding it inside one feature slice.

Read-only cross-slice access is allowed when necessary through a narrow interface. For example, browser automation may need read-only workspace state, but browser automation should not directly mutate workspace registry data.

If a relationship starts to carry its own behavior or lifecycle, promote it to its own slice.

## Relationships

Most relationships should be represented by IDs and owned by the aggregate/capability that controls their lifecycle.

Many-to-many relationships should usually become explicit concepts. For example, if workspaces and applications need a relationship with lifecycle, diagnostics, or state, model that relationship as its own entity/slice rather than hiding it in a join table.

# Server Ownership Rules

The Server owns all orchestration:

- Workspace registry.
- Project path identity and optional Git worktree metadata.
- Process lifecycle.
- Port allocation.
- Docker lifecycle.
- Capability reconciliation and status.
- Browser lifecycle.
- Browser profiles.
- Browser session persistence.
- Event recording.
- Diagnostics.
- Health monitoring.
- Playwright generation.
- MCP server.
- REST API.

No orchestration logic belongs in Desktop, CLI, or MCP clients.

Full guide: `docs/developer-guide/server.md`.

# Client Rules

## Desktop

The Desktop is an Avalonia client for humans. It displays workspaces, browser tabs, logs, diagnostics, health, and running processes.

It connects to the Server and must not own runtime state. Full guide: `docs/developer-guide/desktop.md`.

Installed Desktop packages must install or depend on a local Server service rather than embedding orchestration in the Desktop process.

## CLI

The CLI is a thin developer convenience wrapper over Server capabilities.

It should forward commands such as restart, stop, status, and logs to the Server. User guide: `docs/user-docs/cli.md`.

## MCP

MCP is the primary automation interface for AI agents.

Agents should use MCP directly instead of shelling through the CLI when browser inspection, interaction, diagnostics, logs, screenshots, or Playwright generation are needed. Full guide: `docs/developer-guide/mcp.md`.

Agent-Up MCP initialization instructions must tell clients to use `start_workspace` when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up; this means starting the local managed development environment, not deploying to cloud infrastructure.

# Configuration Rules

Every managed repository is described declaratively with `agent-up.json`.

Applications must not reference Agent-Up packages, SDKs, or APIs. Agent-Up injects runtime values through environment variables and process launch configuration.

Legacy local application commands and legacy Docker `services` remain supported. New ecosystem-aware configuration should prefer capability sections such as `dotnet` and `docker`; the Server reconciles declared version requirements with versions discovered or managed by capability adapters, then exposes capability status to Desktop, CLI, and automation clients.

User docs:

- `docs/user-docs/configuration.md`
- `docs/user-docs/agent-up-json.md`

# Port Allocation

The Server owns all ports.

Each workspace receives a dedicated contiguous port range. Applications consume only environment variables such as `WEB_PORT`, `API_PORT`, and `AUTH_PORT`.

Workspace guide: `docs/user-docs/workspace.md`.

# Browser Model

Each workspace owns an isolated browser profile.

The Server manages browser lifecycle and state; the Desktop displays browser sessions. Browser state includes cookies, local storage, session storage, IndexedDB, cache, and navigation state.

User docs:

- `docs/user-docs/browser.md`
- `docs/user-docs/browser-profiles.md`

# Browser Automation

AI agents interact with applications through Server-backed browser automation.

Prefer structured inspection and accessibility data over raw HTML. Every interaction should be recordable as an event that can later support diagnostics, workflow inference, and Playwright generation.

Developer guides:

- `docs/developer-guide/event-recording.md`
- `docs/developer-guide/playwright.md`

# Diagnostics

Diagnostics are collected continuously by the Server and exposed to Desktop, CLI, and MCP clients.

Diagnostics include console output, JavaScript exceptions, failed network requests, performance timings, health information, and process status.

Full guide: `docs/developer-guide/diagnostics.md`.

# Error Handling And Validation

Use structured application errors at host boundaries.

New code should convert known failures into safe errors with status, title, detail, and validation/error lists where appropriate. Do not allow raw infrastructure, browser, Docker, process, filesystem, or framework exceptions to leak directly through REST or MCP boundaries.

Guidelines:

- Convert provider/infrastructure exceptions at meaningful boundaries.
- Catch specific known exception types. Do not use bare `catch`, unfiltered `catch (Exception)`, or empty catch blocks; best-effort cleanup must log, return a typed result, or use a documented helper.
- When mapping operation cancellation to a timeout, verify the timeout `CancellationTokenSource` fired and preserve caller cancellation separately.
- Validate command runner inputs before process launch. Package smoke command execution must choose from allowlisted command names and must not pass executable paths or unchecked user-provided strings into `ProcessStartInfo`.
- Encode or otherwise canonicalize user-controlled IDs before using them in filesystem paths, and verify the resolved path stays under the owning storage root.
- Use `Path.Join` or an owning path-validation provider instead of `Path.Combine` for repository/runtime paths.
- Dispose local `IDisposable` values with `using`/`await using` unless ownership is intentionally transferred to a longer-lived object.
- Do not block on async work with `.GetAwaiter().GetResult()`, `.Wait()`, or `.Result` in production startup, UI, or composition paths.
- Do not add catch blocks at every layer.
- Validate transport/request models at host boundaries.
- Keep domain/runtime invariants in the owning slice.
- Prefer clear typed results or structured exceptions over stringly-typed failure handling.

Every validation rule that affects public behavior requires a focused test at the boundary where that behavior is observed.

# Testing

Any change to a project that has a corresponding test project must include test changes in the same commit.

This applies to every production/test project pair once created:

| Project | Test Project |
|---|---|
| `AgentUp.Server` | `AgentUp.Server.Tests` |
| `AgentUp.Capabilities.Abstractions` | `AgentUp.Capabilities.Abstractions.Tests` |
| `AgentUp.Capabilities.Common` | `AgentUp.Capabilities.Common.Tests` |
| `AgentUp.Capabilities.Dotnet` | `AgentUp.Capabilities.Dotnet.Tests` |
| `AgentUp.Capabilities.Docker` | `AgentUp.Capabilities.Docker.Tests` |
| `AgentUp.Desktop` | `AgentUp.Desktop.Tests` |
| `AgentUp.CLI` | `AgentUp.CLI.Tests` |
| `AgentUp.Installers` | `AgentUp.Installers.Tests` |
| `AgentUp.InstallerApp` | `AgentUp.InstallerApp.Tests` |
| `AgentUp.Packaging` | `AgentUp.Packaging.Tests` |
| `AgentUp.PackageSmoke` | `AgentUp.PackageSmoke.Tests` |

`AgentUp.Architecture.Tests` is a dedicated ArchUnitNET/NUnit project for executable architecture and review-hygiene rules. It validates production project dependency ownership, feature/type-folder layout, shared-folder layout, controller dependency construction rules, error-handling hygiene, path/disposable/async safety, and test taxonomy rules. Keep architecture and generic source hygiene rules there instead of burying them in product E2E tests.

`AgentUp.Tests` is a separate cross-product E2E project that exercises the full Desktop application and shared Installer application through platform fixture adapters. Linux uses `AgentUp.Fixtures.Linux` with Xvfb and WebKitGTK. macOS uses `AgentUp.Fixtures.MacOs`, and Windows uses `AgentUp.Fixtures.Windows`, each starting Avalonia against the native desktop/WebView backend available on the CI runner. These tests are part of the normal platform test run. macOS CI runs the project through its NUnitLite executable entry point so Avalonia Native initializes on the process main thread while still exercising the same test fixtures and native WebView.

Changes to packaging, installers, CI payload staging, Desktop startup, browser/WebView hosting, or installed app layout that can affect the delivered Desktop or InstallerApp runtime must run the relevant project tests and `AgentUp.Tests` in the same verification pass. Do not claim completion for those changes after only running the package, installer, or app unit test projects.

After every task that touches any production project, run the architecture tests before reporting completion:

```
dotnet test AgentUp.Architecture.Tests/AgentUp.Architecture.Tests.csproj
```

All 8 architecture rules must pass. Fix any violation before considering the task done. Do not move on, commit, or report success while architecture tests are failing.

Forbidden:

- Changing production behavior without updating or adding tests for that behavior.
- Adding REST endpoints or MCP tools without tests for the new contract.
- Changing request/response/resource shapes without updating tests.
- Removing behavior without removing or updating tests that covered it.
- Claiming completion while relevant tests are missing, skipped, or known broken.
- Claiming completion for packaging, installer, Desktop, browser/WebView, or installed-layout changes without running the native-display `AgentUp.Tests` project unless the platform lacks the required native display dependencies; in that case, report the missing dependency and the exact CI-shaped command that still needs to run.

## Test Structure

Tests should follow the same feature/slice layout as production code.

Architecture rules belong in `AgentUp.Architecture.Tests`. Use ArchUnitNET for assembly/type dependency rules and focused filesystem/source checks for physical layout rules ArchUnitNET cannot observe.

```text
AgentUp.Server.Tests/
  Features/
    Workspaces/
      HTTP/
      Unit/
      Repository/
    Applications/
      HTTP/
    Processes/
      Unit/
      TerminalIntegration/
    Browser/
      Automation/
      Unit/
    Mcp/
      Tools/
      Resources/

AgentUp.Desktop.Tests/
  Features/
    Workspaces/
      Headless/     (Avalonia headless tests for sidebar/workspace-list UI)
      Unit/         (ViewModel unit tests, no UI)
      TerminalIntegration/ (tests that inspect real project or filesystem state)
    Applications/
      Headless/     (Avalonia headless tests for application panel UI)
    Console/
      Headless/     (Avalonia headless tests for console output panel UI)
  Support/          (AppDriver, SidebarDriver, ContentDriver, WorkspaceFixtures)
```

## Test Strategy

Use layered tests with clear ownership:

- Unit tests verify domain/runtime rules and edge cases.
- HTTP tests verify REST routing, model binding, validation, status codes, and response shapes.
- MCP tests verify tool/resource contracts and safe errors.
- Repository/infrastructure tests verify persistence behavior with realistic storage dependencies when practical.
- Terminal integration tests verify terminal-like workflows, generated directory state, process-style behavior, package layouts, installer smoke behavior, and post-run filesystem assertions.
- End-to-end workspace lifecycle tests should be few and prove full integration across Server, process management, ports, diagnostics, and browser state.

`Unit/` tests must not use real filesystem, process execution, sockets, current-directory mutation, or environment mutation APIs. If a test needs `File.*`, `Directory.*`, `Path.GetTempPath`, `Process.Start`, `ProcessStartInfo`, `Directory.SetCurrentDirectory`, `Environment.SetEnvironmentVariable`, `TcpListener`, `TcpClient`, or `Socket`, put it in `Repository/`, `Provider/`, `TerminalIntegration/`, `HTTP/`, `Headless/`, or `E2E/` according to the behavior being observed.

Avoid duplicate tests that assert the same rule through multiple layers.

# Content Sections

The sections below intentionally introduce each concept briefly and point to the canonical docs page. Keep AGENTS.md concise; detailed specifications belong in `docs/`.

## Workspace

A workspace is the unit of isolation for an agent or developer session. It is identified by project path and may include repository/worktree metadata, branch, commit, browser profile, Docker infrastructure, running processes, allocated ports, diagnostics, and event history. Non-Git project paths are valid and should display as `not on a git branch`.

Read: `docs/user-docs/workspace.md`.

## Configuration

Agent-Up uses declarative repository configuration through `agent-up.json`. Applications declare launch commands, port environment variables, browser paths, and Docker setup without source-code integration.

Capability sections such as `dotnet` and `docker` are the preferred shape for ecosystem-aware requirements. Capability adapters discover system and Agent-Up-managed versions, reconcile declared requirements, return structured mismatch status, and produce Server-owned launch plans. The legacy `applications` list remains supported for opaque shell commands, and legacy Docker `services` remain supported for compatibility.

Read: `docs/user-docs/configuration.md` and `docs/user-docs/agent-up-json.md`.

## Browser

Agent-Up keeps browser sessions tied to workspaces so developers and agents share authentication and navigation state. Restarting applications should reload the existing workspace browser session rather than create more tabs.

Read: `docs/user-docs/browser.md` and `docs/user-docs/browser-profiles.md`.

## Server

The Server is the runtime authority for Agent-Up. It owns orchestration, state, lifecycle, diagnostics, MCP, and REST APIs.

Read: `docs/developer-guide/server.md`.

## Desktop

The Desktop is the Avalonia UI for humans. It presents Server-owned state and shared browser sessions.

**Per-workspace browser isolation:** Each workspace gets its own `NativeWebView` instance. Isolation is achieved by handling the `EnvironmentRequested` event and assigning platform-native profile storage from `BrowserUrlStore.ProfilePath`: GTK/WPE data and cache directories on Linux, WebView2 user data folders on Windows, and WKWebView data store identifiers on macOS. The last-visited URL per workspace is persisted by `BrowserUrlStore` and restored when the workspace is reopened.

Read: `docs/developer-guide/desktop.md`.

## CLI

The CLI is a convenience client for humans. It forwards commands to the Server and owns no runtime state.

Read: `docs/user-docs/cli.md`.

## MCP

The MCP server is the main automation interface for AI agents. It exposes workspace resources and tools for browser interaction, logs, diagnostics, screenshots, waits, and Playwright export.

Read: `docs/developer-guide/mcp.md`.

## Event Recording

Every browser interaction and relevant runtime signal should become an event. The event stream is the canonical history used for diagnostics, workflow inference, and future automation.

Read: `docs/developer-guide/event-recording.md`.

## Playwright Generation

Playwright tests should be generated from recorded intent and outcomes, not brittle raw click replay. Prefer semantic locators and inferred assertions.

Read: `docs/developer-guide/playwright.md`.

## Diagnostics

Diagnostics make AI validation practical by exposing process, browser, network, console, health, and performance information from the live workspace.

Read: `docs/developer-guide/diagnostics.md`.

## Workflows

The target AI workflow is: modify code, restart workspace, wait until healthy, inspect page, interact, validate, screenshot, generate Playwright, commit.

Read: `docs/developer-guide/workflows.md`.

## Packaging And Installers

Installer and packaging behavior is testable product behavior. Shared installer planning, payload, adapter, progress, validation, per-component install/update/uninstall/repair, and platform install contracts belong in `AgentUp.Installers`, with matching tests in `AgentUp.Installers.Tests`. The shared InstallerApp UX is a dashboard for managing Desktop, Server, CLI, and capability modules, with Avalonia headless tests in `AgentUp.InstallerApp.Tests` and native-display flow tests in `AgentUp.Tests`; the installer app uses real platform adapters by default when `AGENTUP_INSTALLER_PAYLOAD_ROOT` points at a staged payload, supports noninteractive operation smoke through `AgentUp.InstallerApp --smoke-installer-operations --payload-root <payload-root>` that exercises individual component operations before bundled core install, and tests opt into fake adapters with `AGENTUP_INSTALLER_FAKE=1`. Native package formats should wrap or launch that dashboard rather than owning divergent install flows. Release artifact staging, package metadata generation, and native packaging tool orchestration belongs in `AgentUp.Packaging`, with matching tests in `AgentUp.Packaging.Tests`; packaging code must consume shared installer contracts instead of redefining platform behavior. CI packaging must use prebuilt InstallerApp, Desktop, Server, CLI, Packaging, and PackageSmoke artifacts from the Ubuntu build job so native release runners do not restore, build, or test product .NET projects. Shared package and installed-service smoke validation belongs in `AgentUp.PackageSmoke`, with matching tests in `AgentUp.PackageSmoke.Tests`; CI smoke scripts should launch the packaged InstallerApp when `AGENTUP_INSTALLER_APP_COMMAND` is set and delegate native artifact, install, service, CLI, diagnostics, and uninstall checks to PackageSmoke. Native package assets stay under `packaging/` and should consume shared installer contracts rather than accumulating untested script-only behavior.

Windows package product identity must come from the product manifest: WiX product and bundle metadata, service name, CLI shim name, registry keys, shortcuts, upgrade GUID, MSI sidecar name, and bootstrapper name are product-branded. The Agent-Up manifest must continue to produce the existing `agent-up-windows-<rid>` artifact names and WiX command shape.

All `AgentUp.Packaging` filesystem access must pass through shared path validation in `Shared/Providers/PackagePathValidator` before reading, writing, copying, deleting, or creating directories. Package output directories are repository-relative and must remain under the repository root; prebuilt payload roots may be absolute CI-provided paths or repository-relative paths normalized under the repository root.

All `AgentUp.PackageSmoke` process execution must pass through validated command providers. Smoke validation may execute native package managers, service tools, installed CLIs, Git, and capability-backed sample app lifecycle commands, but execution must choose from allowlisted command names before `ProcessStartInfo` is created. Artifact paths, installed executable paths, working directories, arguments, and environment keys stay data and must be validated before use.

macOS `.pkg` artifacts install only `Agent-Up Installer.app`. The installer app owns the dashboard install and maintenance flow and contains a bundled offline payload with Desktop, Server, and CLI bits; it may also resolve an online latest payload when that capability is implemented. Desktop, Server, CLI, launchd registration, symlinks, validation, and uninstall behavior must stay in the InstallerApp/macOS adapter path, not in direct macOS package components. macOS installed-service smoke is skipped until InstallerApp-driven service installation is enabled in CI after package installation.

Packaging from NixOS or other non-native hosts should use the wrapper scripts in `scripts/package-*.sh`, which enter target-specific shells from `packaging/nix/` before delegating to the packaging entrypoint. NixOS installs Agent-Up declaratively through generated NixOS/Home Manager module options; `AgentUp.InstallerApp` is still shipped as a lookup-only dashboard through `agent-up-installer`, with install/update/uninstall actions disabled and capability versions read from Agent-Up capability inventory. Runtime capability lookup reads `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then falls back to `/etc/agent-up/capabilities.json` and `~/.config/agent-up/capabilities.json`; first-party capability discovery also probes common platform package-manager records for .NET and Docker. Installed-service smoke launches one .NET app and one Docker app through capability declarations and validates individual app stop/start plus workspace stop unless `AGENTUP_CAPABILITY_SMOKE_SKIP_REAL=1` is set for constrained runs; the Docker sample uses `nginx:alpine` on Linux and macOS and a matching Windows IIS image on Windows runners, with `AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE` available for CI pre-pull/override. macOS packaging still requires Darwin because Apple package, signing, and notarization tools are not available on Linux.

Read: `docs/developer-guide/packaging.md`.

## Design Principles

Agent-Up must remain framework agnostic, cross-platform, declarative, and zero-touch for application source code.

Read: `docs/developer-guide/design-principles.md`.

## Roadmap

Agent-Up should evolve into the runtime operating system for AI-assisted development while Git manages source, Docker manages containers, and IDEs manage editing.

Read: `docs/user-docs/roadmap.md`.
