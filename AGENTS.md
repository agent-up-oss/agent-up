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

AgentUp.Mobile/
  package.json

AgentUp.CLI/
  AgentUp.CLI.csproj

AgentUp.CommitPolicy/
  AgentUp.CommitPolicy.csproj

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

AgentUp.CommitPolicy.Tests/
  AgentUp.CommitPolicy.Tests.csproj

AgentUp.Architecture.Tests/
  AgentUp.Architecture.Tests.csproj

AgentUp.Tests/
  AgentUp.Tests.csproj
```

Project directories live directly at the repository root and are included in the appropriate solution. Do not introduce `src/` or `tests/` wrapper directories unless the repository is intentionally reorganized everywhere. `agent-up.sln` references only Agent-Up product and test projects; Agent-Up projects consume LocalInstaller through `LocalInstaller.*` NuGet packages pinned by `$(LocalInstallerVersion)`. The LocalInstaller source, tests, samples, and `localinstaller.sln` live in the sibling LocalInstaller repository.

The exact project list may evolve, but ownership must not drift:

| Area | Owns |
|---|---|
| `AgentUp.Server` | Workspace registry, process lifecycle, ports, Docker, browser lifecycle, diagnostics, event recording, MCP, REST API |
| `AgentUp.Capabilities.Abstractions` | Stable capability adapter interfaces, manifest DTOs, installed-version inventory contracts, validation results, and launch plans |
| `AgentUp.Capabilities.Common` | Shared capability catalog parsing, checksum validation, Agent-Up tool-cache layout, and install planning used by first-party and future external capabilities |
| `AgentUp.Capabilities.Dotnet` | First-party .NET ecosystem adapter, SDK discovery, version reconciliation, and `dotnet` launch planning |
| `AgentUp.Capabilities.Docker` | First-party Docker ecosystem adapter, Docker discovery, validation, and Docker launch planning |
| `AgentUp.Desktop` | Avalonia UI, workspace display, logs, diagnostics, embedded/shared browser views |
| `AgentUp.Mobile/` | Expo and React Native client for Android, iOS, and the installable web PWA; displays Server-owned state and submits user requests |
| `AgentUp.CLI` | Thin human-friendly command wrapper over Server capabilities |
| `AgentUp.CommitPolicy` | Shared commit-message prefix, scope, and file-classification policy used by Server MCP and CLI local commit queues |
| `LocalInstaller.Core` | Product-neutral installer prerequisite, component selection, PATH, validation, and uninstall planning contracts |
| `LocalInstaller.App` | Product-neutral Avalonia installer dashboard over platform installer adapters and installer-owned capability catalog state; no compile-time dependency on `AgentUp.Capabilities.*` |
| `LocalInstaller.Packaging` | Product-neutral release artifact staging, package metadata generation, and native packaging tool orchestration |
| `LocalInstaller.Smoke` | Product-neutral package and installed-service smoke validation adapters used by CI smoke scripts |
| `AgentUp.InstallerApp` | Thin Agent-Up installer entrypoint that registers the Agent-Up product manifest through LocalInstaller |
| `AgentUp.Packaging` | Thin Agent-Up packaging entrypoint that registers the Agent-Up package manifest through LocalInstaller |
| `AgentUp.PackageSmoke` | Thin Agent-Up smoke entrypoint that registers the Agent-Up smoke manifest through LocalInstaller |
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
    Orchestration/    (cross-capability workspace and Agent-Up context operations)
      Controllers/
      DTOs/
      Interfaces/
      Providers/
      Services/

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

AgentUp.Mobile/
  src/
    app/               (Expo Router entrypoints only)
    features/          (product-meaningful React Native client slices)

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
    Commits/          (local vertical-slice commit staging queue, no Server dependency)
      Controllers/
      DTOs/
      Interfaces/
      Models/
      Providers/
      Services/

LocalInstaller.Core/
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

LocalInstaller.Packaging/
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

LocalInstaller.Smoke/
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
    Orchestration/
      Controller/
      Unit/
      Provider/
      HTTP/
```

Prefer working only in the slice directly involved in the task.

Feature slice names should have product, customer, operator, or maintainer meaning. Avoid creating top-level slices for tiny technical mechanisms such as payload parsing, PATH editing, execution helpers, or validation records when they are only part of a larger capability; keep those as type-folder contents inside the meaningful owning slice.

## Migrations And Persistence

If persistent storage is introduced, migrations stay together in the owning infrastructure/migration location for the project.

Feature separation happens at repository/service boundaries. Do not scatter migration files by feature unless the project explicitly adopts that convention later.

## Inter-Slice Communication

A slice owns its writes.

Project entrypoints such as `Program.cs`, host routes, CLI commands, MCP tools, and UI event handlers should call into a slice through `Controllers/`, either directly or through the project composition root that exposes those controllers. Controllers receive dependencies through constructors; they must not create services or providers. Keep controllers thin: they map external calls and DTO arguments to injected services.

MCP is a protocol surface, not a feature slice. MCP tools and resources live in the owning feature slice's `Controllers/` folder as thin protocol adapters. Cross-capability workspace management and Agent-Up context tools belong to the `Orchestration` slice. Slice-specific tools, such as commit queue tools, belong to their owning slice.

Services own domain lifecycle and orchestration behind controllers. Services may call same-slice repositories, providers, factories, and models, but they must stay domain-specific. Services must not contain low-level parsing, command construction, filesystem/archive operations, native tool invocation, environment lookup, HTTP/network mechanics, process execution, platform API calls, XML/manifest serialization mechanics, or string-scanning helpers for external tool output. Put that behavior behind same-slice `Providers/` with names that describe the user/operator capability where practical, such as `PackageCommandParser`, `DpkgDebPackageTool`, `WindowsWixPackagingTool`, `MacOsPackageArchiveProvider`, or `DockerPrerequisiteProvider`.

Use `Models/` for data definitions and pure internal representations that stay inside the slice, including generated manifest/script/XML text when the code is defining package or installer data rather than performing I/O. Use `DTOs/` only for data crossing external or controller boundaries.

Provider interfaces are justified when they hide low-level providers from services, are faked by tests, or select runtime adapters. A service depending on `IUbuntuPackageTool` is acceptable; a service building `new CommandSpec("dpkg-deb", ...)` is not. A controller or service parsing raw `string[] args` is not acceptable; use a parser provider that returns a DTO/result.

Slices must not reach directly into another slice's internal `Services/`, `Models/`, `Providers/`, `Interfaces/`, `Repositories/`, `Factories/`, `Tools/`, `Views/`, or `ViewModels/`. Cross-slice calls go through the target slice's `Controllers/` boundary and exchange IDs or `DTOs/`. If a low-level abstraction or read-only contract is genuinely shared by multiple slices, place it in a project-level `Shared/` folder instead of hiding it inside one feature slice.

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

## Mobile

The mobile client is a single Expo and React Native TypeScript project that targets Android, iOS, and an installable web PWA. It lives in `AgentUp.Mobile/` at the repository root and is not part of `agent-up.sln`.

Mobile route entrypoints stay thin under `src/app/`; product UI and client behavior live in capability-oriented slices under `src/features/`. Do not commit Expo-generated `android/` or `ios/` projects unless native customization is intentionally adopted. The mobile client displays Server-owned state and must not own orchestration.

Developer guide: `docs/developer-guide/mobile.md`.

## MCP

MCP is the primary automation interface for AI agents.

Agents should use MCP directly instead of shelling through the CLI when browser inspection, interaction, diagnostics, logs, screenshots, or Playwright generation are needed. Full guide: `docs/developer-guide/mcp.md`.

Agent-Up MCP initialization instructions must tell clients to use `start_workspace` immediately when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up; this means starting the local managed development environment, not deploying to cloud infrastructure. Agents should not call `list_workspaces` or `get_workspace_status` first when the current repository/worktree is known.

# Configuration Rules

Every managed repository is described declaratively with `agent-up.json`.

Applications must not reference Agent-Up packages, SDKs, or APIs. Agent-Up injects runtime values through environment variables and process launch configuration.

Legacy local application commands and legacy Docker `services` remain supported. Local application commands are executable-plus-arguments strings, not shell expressions; the Server launches them directly with an argument list and rejects shell chaining, redirects, variable expansion, and subshells. New ecosystem-aware configuration should prefer capability sections such as `dotnet` and `docker`; the Server reconciles declared version requirements with versions discovered or managed by capability adapters, then exposes capability status to Desktop, CLI, and automation clients.

The optional root `display` object in `agent-up.json` is only for Desktop visuals. `display.name` overrides the workspace entry title and `display.branch` overrides the workspace entry subtitle. These values must not change repository path identity, worktree path handling, Git branch detection, commit identity, audit identity, or process working directories.

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
| `LocalInstaller.Core` | `LocalInstaller.Core.Tests` |
| `LocalInstaller.App` | `LocalInstaller.App.Tests` |
| `LocalInstaller.Packaging` | `LocalInstaller.Packaging.Tests` |
| `LocalInstaller.Smoke` | `LocalInstaller.Smoke.Tests` |

`AgentUp.Architecture.Tests` is a dedicated ArchUnitNET/NUnit project for executable architecture and review-hygiene rules over source owned by the Agent-Up repository. It validates production project dependency ownership, feature/type-folder layout, shared-folder layout, concrete controller boundary presence for slices with inbound traffic, controller dependency construction rules, controller separation from providers/repositories/factories, controller and service sibling-slice boundary usage, controller method complexity, nested production type bans, feature test-kind coverage, error-handling hygiene, path/disposable/async safety, and test taxonomy rules. LocalInstaller source architecture is tested in the sibling LocalInstaller repository. Keep architecture and generic source hygiene rules in the owning repository instead of burying them in product E2E tests.

`AgentUp.Tests` is a separate cross-product E2E project that exercises the full Desktop application and shared Installer application through platform fixture adapters. Linux uses `AgentUp.Fixtures.Linux` with Xvfb and WebKitGTK. macOS uses `AgentUp.Fixtures.MacOs`, and Windows uses `AgentUp.Fixtures.Windows`, each starting Avalonia against the native desktop/WebView backend available on the CI runner. These tests are part of the normal platform test run. macOS CI runs the project through its NUnitLite executable entry point so Avalonia Native initializes on the process main thread while still exercising the same test fixtures and native WebView.

Changes to packaging, installers, CI payload staging, Desktop startup, browser/WebView hosting, or installed app layout that can affect the delivered Desktop or InstallerApp runtime must run the relevant project tests and `AgentUp.Tests` in the same verification pass. Do not claim completion for those changes after only running the package, installer, or app unit test projects.

After every task that touches any production project, run the architecture tests before reporting completion:

```
dotnet test AgentUp.Architecture.Tests/AgentUp.Architecture.Tests.csproj
```

All architecture rules must pass. Fix any violation before considering the task done. Do not move on, commit, or report success while architecture tests are failing.

Changes under `AgentUp.Mobile/` must run `npm run typecheck` and `npm run build:web` from that directory. Add focused client tests with new behavior once the corresponding test boundary exists; a static export alone must not substitute for behavior tests.

Every public mobile npm script must invoke its Expo or TypeScript command through the repository `shell.nix`. Do not add duplicate direct or `:nix` script variants. Keep Node.js, `NIX_LD`, `patchelf`, the DotSlash DevTools preparation, and the React Native DevTools Electron runtime libraries in `shell.nix` so NixOS launches use the same reproducible environment.

Mobile development servers use Expo LAN mode so Metro is reachable through the host network. Production web builds must export through Metro. Keep the web manifest, install icons, production-only service-worker registration, and stable updater service worker synchronized. Ticket-number-prefixed branches are mobile release channels; their immutable GitHub pre-releases contain the complete Metro payload and metadata, while the updater service worker changes only when its bootstrap protocol changes.

Forbidden:

- Changing production behavior without updating or adding tests for that behavior.
- Adding REST endpoints or MCP tools without tests for the new contract.
- Changing request/response/resource shapes without updating tests.
- Removing behavior without removing or updating tests that covered it.
- Claiming completion while relevant tests are missing, skipped, or known broken.
- Claiming completion for packaging, installer, Desktop, browser/WebView, or installed-layout changes without running the native-display `AgentUp.Tests` project unless the platform lacks the required native display dependencies; in that case, report the missing dependency and the exact CI-shaped command that still needs to run.

## Test Structure

Tests should follow the same feature/slice layout as production code.

Architecture rules belong in `AgentUp.Architecture.Tests`. Use ArchUnitNET for assembly/type dependency rules and focused filesystem/source checks for physical layout rules ArchUnitNET cannot observe. Root-level test support folders are limited to documented support areas such as `Support/`, `Fixtures/`, `Fake/`, `Architecture/`, or root `E2E/`; test-kind folders such as `Controller/` must stay under `Features/<Slice>/`.

Feature slices with `Controllers/`, `Services/` or `Models/`, and `Providers/` should have matching `Controller/`, `Unit/`, and `Provider/` test-kind coverage. Existing gaps are tracked as explicit architecture-test debt; new or expanded slices must not add to that baseline.

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
      Provider/
    Browser/
      Provider/
      Unit/
    Orchestration/
      Controller/
      Unit/
      Provider/
      HTTP/

AgentUp.Desktop.Tests/
  Features/
    Workspaces/
      Headless/     (Avalonia headless tests for sidebar/workspace-list UI)
      Unit/         (ViewModel unit tests, no UI)
      Provider/     (tests for low-level providers, filesystem/project-icon adapters, and similar boundaries)
    Applications/
      Headless/     (Avalonia headless tests for application panel UI)
    Console/
      Headless/     (Avalonia headless tests for console output panel UI)
  Support/          (AppDriver, SidebarDriver, ContentDriver, WorkspaceFixtures)
```

## Test Strategy

Use layered tests with clear ownership:

- Unit tests verify domain/runtime rules and edge cases.
- Controller tests verify slice-external communication boundaries such as controllers, command parsers, CLI command surfaces, MCP tools, and MCP resources with repositories and providers mocked or faked.
- HTTP tests verify REST routing, model binding, validation, status codes, and response shapes.
- Repository/infrastructure tests verify persistence behavior with realistic storage dependencies when practical.
- Provider tests verify low-level external behavior in isolation, including filesystem providers, command/tool providers, environment providers, platform adapters, package writers/stagers, probes, generated directory state, and process-style command shapes. Temp directories are allowed when the provider boundary requires them.
- Headless tests verify Avalonia UI behavior without native display dependencies.
- End-to-end workspace lifecycle tests should be few and prove full integration across Server, process management, ports, diagnostics, and browser state.

`Unit/` tests must not use real filesystem, process execution, sockets, current-directory mutation, or environment mutation APIs. If a test needs `File.*`, `Directory.*`, `Path.GetTempPath`, `Process.Start`, `ProcessStartInfo`, `Directory.SetCurrentDirectory`, `Environment.SetEnvironmentVariable`, `TcpListener`, `TcpClient`, or `Socket`, put it in `Repository/`, `Provider/`, `HTTP/`, `Headless/`, or `E2E/` according to the behavior being observed.

Avoid duplicate tests that assert the same rule through multiple layers.

# Content Sections

The sections below intentionally introduce each concept briefly and point to the canonical docs page. Keep AGENTS.md concise; detailed specifications belong in `docs/`.

## Workspace

A workspace is the unit of isolation for an agent or developer session. It is identified by project path and may include repository/worktree metadata, branch, commit, browser profile, Docker infrastructure, running processes, allocated ports, diagnostics, and event history. Non-Git project paths are valid and should display as `not on a git branch`.

Read: `docs/user-docs/workspace.md`.

## Configuration

Agent-Up uses declarative repository configuration through `agent-up.json`. Applications declare launch commands, port environment variables, browser paths, and Docker setup without source-code integration.

Capability sections such as `dotnet` and `docker` are the preferred shape for ecosystem-aware requirements. Capability adapters discover system and Agent-Up-managed versions, reconcile declared requirements, return structured mismatch status, and produce Server-owned launch plans. The legacy `applications` list remains supported for executable-plus-arguments commands, and legacy Docker `services` remain supported for compatibility.

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

The MCP servers are the main automation interface for AI agents. The Server exposes Orchestration MCP at `/mcp/orchestration` for workspace resources, orchestration tools, and live workspace console snapshots; Browser MCP at `/mcp/browser` for browser automation; Audit MCP at `/mcp/audit` for durable action history and artifacts; and Commits MCP at `/mcp/commits` for commit queue tools. Clients must connect to the specific MCP server they need instead of the former shared `/mcp` endpoint.

Agent-Up validation is a feedback loop: call `start_workspace`, use the returned workspace id and allocated ports for Browser MCP validation, and if browser navigation, inspection, waiting, screenshots, or interaction fails or times out, inspect the workspace console first through Orchestration MCP `get_workspace_console`. If that tool is unavailable, query Audit MCP for recent `application` events from `process` for the workspace before trying more browser actions. Console output is the first diagnostic source for missing dependencies, failed commands, port binding errors, Docker startup failures, and build/runtime crashes.

Read: `docs/developer-guide/mcp.md`.

## Event Recording

Every browser interaction and relevant runtime signal should become an event. The event stream is the canonical history used for diagnostics, workflow inference, and future automation.

Browser navigation is restricted to loopback URLs on the workspace's allocated HTTP application ports unless an explicit external allowlist is introduced for flows such as OAuth providers. Browser screenshots are Server-managed audit artifacts. Screenshot tools should return bounded MCP image content for immediate agent inspection plus an opaque artifact id for later Audit MCP lookup, not temporary filesystem paths. Captured application console lines should be mirrored into durable audit events without breaking the active workspace session if audit recording fails.

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

## Commit Workflow

Coding agents must not run `git commit`, `git add`, or `git stash` directly. Instead, use the MCP `enqueue_commit` tool to declare each vertical-slice commit at the end of a task. Use `enqueue_review_fix_commit` when fixing a pull request review issue; each queued review-fix entry must represent exactly one review issue id. The developer then runs `agentup commits next` to stage each entry in isolation, reviews the diff in their editor, and commits manually.

Agent responsibility: manage queue entries only through structured MCP commit queue tools. Never `commits next`, never `git add`, never `git commit`, never `git stash`. After all enqueue or queue-editing calls, run `get_commits_status` so the developer can see the queue — then stop. The developer runs `commits next` themselves.

Before starting a new coding task, agents should run the structured MCP `guard_commits` tool for the current repository/worktree. If it fails, stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

Agents should use structured commit queue MCP tools for enqueue, queue inspection, metadata edits, file assignment, edit sessions, archive/restore, clear, and guard operations instead of shelling through commit CLI commands. `commits next` remains developer-only because it stages files and advances the review queue.

The MCP `enqueue_commit` tool intentionally restores tracked files to their pre-change state after saving the queued patch. Agents must treat that restoration as expected queue behavior and must not re-apply or modify those files after a successful enqueue; the queue owns them until the developer runs `agentup commits next`.

MCP servers cannot register server-side post-job lifecycle hooks. Claude Code users can wire a client-side `Stop` hook to run `agentup commits guard` and print a reminder when tracked files are dirty but not assigned to a queued entry:

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

```bash
dotnet run --project AgentUp.CLI -- commits enqueue \
  --slice <SliceName> \
  --message "<conventional commit message>" \
  --files <file1> [file2 ...] \
  [--tests "<test command>"]
```

The CLI example above is developer-only. Agents use `enqueue_commit`.

One `enqueue` call per logical vertical slice. All files for a slice go in a single entry. Scope each conventional commit message to the queued slice, and follow any repository-specific `prompts.commitPolicy` guidance in `agent-up.json`. Cross-slice guidance or documentation updates must be queued in a separate guidance/docs entry instead of being bundled into an implementation slice. When feature-sliced paths under `Features/<Slice>/` are present, MCP enqueue tools reject cross-slice file groups and mismatched slice labels. Enqueue entries in the order they should be committed.

Mutating commit queue operations are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect in progress. Finish or abort that Git operation before changing or advancing the queue.

Use `agentup commits changes` to inspect the working tree and queue assignment instead of composing raw `git ls-files`, `find`, `grep`, `tr`, or similar shell pipelines.

Queued entries must be manipulated through the commit queue commands:

```bash
agentup commits inspect <entry>
agentup commits message <entry> --message "<conventional commit message>"
agentup commits tests <entry> --set "<test command>"
agentup commits files <entry> --add <file1> [file2 ...]
agentup commits files <entry> --remove <file1> [file2 ...]
agentup commits remove <entry>
agentup commits restore <entry-id>
```

To change an existing queued patch, use an explicit edit session:

```bash
agentup commits edit begin <entry>
# modify only files owned by that entry
agentup commits edit save
```

The working tree must be clean before starting an edit session. `edit save` rejects cross-cutting changes outside the entry's file list; add same-slice files with `agentup commits files <entry> --add ...` before saving. Use `agentup commits edit abort` to discard the working-tree edit and keep the original queued patch.

Before any operation that would publish work outside the local workspace, run:

```bash
agentup commits guard
```

If the guard reports queued entries, an active edit session, staged changes, or unassigned changes, stop and ask the developer to commit or resolve the queued work first.

### Conventional Commit Prefixes

Use the correct prefix — the choice signals intent to reviewers and changelog tooling:

| Prefix | When to use |
|--------|-------------|
| `feat` | User-facing addition |
| `fix` | User-facing fix |
| `test` | Test-only or smoke-validation change |
| `chore` | Maintenance, packaging, CI, or tooling change with no customer runtime effect |
| `refactor` | Internal source change with no behavior change |
| `style` | CSS/HTML only |
| `docs` | Documentation-only change, including README and similar docs |

Scope commit messages to the queued slice, for example `fix(UbuntuInstallation): cover tray autostart boundary`. **Never use `feat` for internal fixes**, even when the fix introduces a new guard, method, or type. Production changes in Server, CLI, Tray, InstallerApp, Installers, or Desktop are customer-facing and should be `fix` or `feat` unless they are true no-behavior source refactors. Test-only changes and PackageSmoke changes use `test` unless they accompany same-slice `feat` or `fix` production changes in the same queued entry. When in doubt, choose the prefix by user-visible intent first and file type second.

## Packaging And Installers

Installer and packaging behavior is testable product behavior. Shared installer planning, payload, adapter, progress, validation, per-component install/update/uninstall/repair, and platform install contracts belong in `LocalInstaller.Core`, with matching tests in `LocalInstaller.Core.Tests`. The shared InstallerApp UX belongs in `LocalInstaller.App`, with Avalonia headless tests in `LocalInstaller.App.Tests` and native-display Agent-Up flow tests in `AgentUp.Tests`; the dashboard includes an explicit refresh action that rechecks installed component and capability-module state for newly available versions. Product entrypoints use the LocalInstaller fluent API to register typed product and artifact manifests; each installable executable owns its artifact manifest, and `Program.cs` files should stay limited to product, installer option, and app startup configuration with no platform-specific installer plumbing. Multiple installer options may share a target category such as CLI or Server, but each option must have a unique artifact ID and payload directory. The installer app uses real platform adapters by default when `AGENTUP_INSTALLER_PAYLOAD_ROOT` points at a staged payload, supports noninteractive operation smoke through `AgentUp.InstallerApp --smoke-installer-operations --payload-root <payload-root>` that exercises individual component operations before bundled core install, treats Server as including tray payload and login autostart, and tests opt into fake adapters with `AGENTUP_INSTALLER_FAKE=1`. Native package formats should wrap or launch that dashboard rather than owning divergent install flows. Ubuntu package postinstall must install the dashboard launcher without auto-launching it; Ubuntu Desktop and InstallerApp launchers declare `StartupWMClass` for taskbar icon matching. Windows installer-owned tray autostart is machine-level so elevated install context does not register only the administrator user. Release artifact staging, package metadata generation, and native packaging tool orchestration belongs in `LocalInstaller.Packaging`, with matching tests in `LocalInstaller.Packaging.Tests`; thin `AgentUp.Packaging` only registers Agent-Up product metadata and delegates to LocalInstaller. CI packaging must use prebuilt InstallerApp, Desktop, Server, CLI, Tray, Packaging, and PackageSmoke artifacts from the Ubuntu build job so native release runners do not restore, build, or test product .NET projects. CI builds `Plugins/Jetbrains` with the planned release version injected through Gradle and publishes `agent-up-jetbrains-plugin.zip` as a GitHub release asset. When `JETBRAINS_MARKETPLACE_TOKEN` is configured, CI also publishes the JetBrains plugin to Marketplace after the GitHub release succeeds. Shared package and installed-service smoke validation belongs in `LocalInstaller.Smoke`, with matching tests in `LocalInstaller.Smoke.Tests`; thin `AgentUp.PackageSmoke` only registers Agent-Up smoke product metadata and delegates to LocalInstaller. PackageSmoke accepts `--product-manifest <path>` so package, installed-service, and installer-flow smoke can run for a second product without recompilation. Installed-service smoke installs the native package, runs the installed InstallerApp with its installed payload root and `--install-core`, then delegates service, CLI, diagnostics, and uninstall checks to PackageSmoke. Native package assets stay under `packaging/` and should consume shared installer contracts rather than accumulating untested script-only behavior.

The standalone LocalInstaller repository owns the LocalInstaller release workflow. It plans versions with semantic-release using `localinstaller-v${version}` tags, builds/tests/packs `localinstaller.sln` with the planned `LocalInstallerVersion`, publishes self-contained `LocalInstaller.Sample.*` payloads from the Ubuntu build leg, packages those sample payloads on native Ubuntu, macOS, and Windows runners through `LocalInstaller.Sample.Packager`, smoke-validates them through `LocalInstaller.Sample.Smoke`, and creates a `main`-only GitHub release containing `LocalInstaller.*.nupkg` plus sample native installer assets. NuGet publishing is optional and must run only when `NUGET_API_KEY` is configured.

Windows package product identity must come from the product manifest: WiX product and bundle metadata, service name, safe CLI shim filename, registry keys, shortcuts, upgrade GUID, product-scoped component and bundle GUIDs, MSI sidecar name, and bootstrapper name are product-branded. The Agent-Up manifest must continue to produce the existing `agent-up-windows-<rid>` artifact names and WiX command shape.

Packaging request/product DTOs belong to `LocalInstaller.Packaging`; packaging code may map them to explicit platform installer contracts but must not depend on installer workflow product/session internals. Package request boundaries must validate the complete product manifest before artifact names, install paths, WiX identity, service names, shim filenames, server URLs, or command arguments are generated.

All `LocalInstaller.Packaging` filesystem access must pass through shared path validation in `Shared/Providers/PackagePathValidator` before reading, writing, copying, deleting, or creating directories. Package output directories are repository-relative and must remain under the repository root; prebuilt payload roots may be absolute CI-provided paths or repository-relative paths normalized under the repository root.

Product packaging wrappers must set `LOCALINSTALLER_REPOSITORY_ROOT` to the product repository root before invoking a published packaging entrypoint, because self-contained executables run from their extraction directory.

All `LocalInstaller.Smoke` process execution must pass through validated command providers. Smoke validation may execute native package managers, service tools, installed CLIs, Git, and capability-backed sample app lifecycle commands, but execution must choose from allowlisted command names before `ProcessStartInfo` is created. Artifact paths, installed executable paths, working directories, arguments, product metadata, and environment keys stay data and must be validated before use.

macOS `.pkg` artifacts install only `Agent-Up Installer.app`. The installer app owns the dashboard install and maintenance flow and contains a bundled offline payload with Desktop, Server, and CLI bits; it may also resolve an online latest payload when that capability is implemented. Desktop, Server, CLI, launchd registration, symlinks, validation, and uninstall behavior must stay in the InstallerApp/macOS adapter path, not in direct macOS package components. macOS installed-service smoke is skipped until InstallerApp-driven service installation is enabled in CI after package installation.

Packaging from NixOS or other non-native hosts should use the wrapper scripts in `scripts/package-*.sh`, which enter target-specific shells from `packaging/nix/` before delegating to the packaging entrypoint. NixOS installs Agent-Up declaratively through generated NixOS/Home Manager module options; `AgentUp.InstallerApp` is still shipped as a lookup-only dashboard through `agent-up-installer`, with install/update/uninstall actions disabled and capability versions read from Agent-Up capability inventory. Runtime capability lookup reads `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then falls back to `/etc/agent-up/capabilities.json` and `~/.config/agent-up/capabilities.json`; first-party capability discovery also probes common platform package-manager records for .NET and Docker. Installed-service smoke launches one .NET app and one Docker app through capability declarations and validates individual app stop/start plus workspace stop unless `AGENTUP_CAPABILITY_SMOKE_SKIP_REAL=1` is set for constrained runs; the generated .NET smoke app is restored and built before `agent-up start` so lifecycle validation does not depend on first-run SDK restore/build timing. The Docker sample uses `nginx:alpine` on Linux and macOS and a matching Windows IIS image on Windows runners, with `AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE` available for CI pre-pull/override. macOS packaging still requires Darwin because Apple package, signing, and notarization tools are not available on Linux.

Read: `docs/developer-guide/packaging.md`.

## Design Principles

Agent-Up must remain framework agnostic, cross-platform, declarative, and zero-touch for application source code.

Read: `docs/developer-guide/design-principles.md`.

## Roadmap

Agent-Up should evolve into the runtime operating system for AI-assisted development while Git manages source, Docker manages containers, and IDEs manage editing.

Read: `docs/user-docs/roadmap.md`.
