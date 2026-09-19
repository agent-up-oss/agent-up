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

Product names that appear in more than one surface:

- Desktop Git chrome is the **Commit** tab; the heading inside it is **Git changes**. Mobile chrome is the **Git** tab, with inner **Review** and **History** pages. Do not call the Desktop tab "Git" or the Mobile tab "Commit".
- The human Git slice is working-tree review. The agent queue is the **commit queue** (legacy local) or **proposal queue** when `commits.enabled` is true. Desktop Commit and Mobile Review display that queue; they do not mutate it.
- Desktop chrome for the live ACP session is the **Agent** tab. Mobile chrome for the picker is the **Agents** tab.
- The installed CLI is `agent-up`. Do not document `agentup` or `agent-up register`.
- Packaged Server URL is `http://localhost:5000`. The repository launch profile is `http://localhost:5001`.
- MCP clients connect to named servers (`/mcp/orchestration`, `/mcp/browser`, `/mcp/audit`, `/mcp/commits`, `/mcp/verification`), never a shared `/mcp`.

Client and platform names (Desktop, Mobile, Server, CLI, Packaging, CI) are glossary and chrome terms. They cannot be sidebar categories. Slice documentation lives on User General and Developer General pages; see Docs IA.

# Docs IA

User Docs (`docs/user-docs/`) and the Developer Guide (`docs/developer-guide/`) share one slice taxonomy. A slice is a user-meaningful capability. Clients (Desktop, Mobile, CLI, MCP) and platforms (Server, CI, Packaging) are never sidebar category names.

Shared slice order:

1. Workspaces
2. Applications
3. Git
4. Commits
5. Agents
6. Browser
7. Diagnostics
8. Verification
9. Configuration

Each slice dropdown's first item is **General**:

- User: `docs/user-docs/<slice>/index.md`
- Developer: `docs/developer-guide/<slice>/index.md`

One non-slice category is allowed:

- User Docs: **Start** — Downloads, Setup, Releases, Limitations, Roadmap
- Developer Guide: **Repo** — architecture rules, packaging, CI, `au-debug`, design-system consumption, telemetry

Definition sources for a slice are that pair of General pages, not a client-named page. MCP is a protocol: each developer General lists that slice's tools and routes. A one-line attach note lives on the Developer Guide index.

## Page standard

Every General uses this reading order, encoded as design-system docs components:

1. `DocEyebrow` — slice name plus `Available` / `Preview` / `Experimental` / `Planned`
2. page title — larger than product chrome (`clamp(2.25rem, 4vw, 2.75rem)`, weight 700)
3. `DocWhat` — what this page is, for a first-time reader, three sentences max. This is the dek under the title. Do not open with a Remember pane.
4. Developer only: `DocMeta` — owner, tests, MCP, REST as a quiet definition list, not a card
5. optional `DocCallout` / `DocFocus` — a rule that only makes sense after the dek. Never the first content. A warning may hold `DocFact` rows when the caution is a list, such as packaged versus repository Server URLs. Do not put those gotchas in `DocFacts`.
6. `DocSpine` — three equal beats. No default highlight. Highlight a beat only when it is the current step of an in-page procedure.
7. `DocContract` — the one featured command, JSON key, or route
8. optional `DocFork`, `DocFacts`, `DocSurfaces` — only where they add information the dek did not already say
9. body sections as `h2`s — `DocSteps`, `DocFacts`, and `DocCallout` instead of undifferentiated paragraphs and CLI dumps
10. `DocNext` — last on the page, after the body

Subpages in a slice dropdown may assume the slice name from General, not the whole General. They still open with what *this* page is.

Give each structural block enough space and a distinct treatment so it reads as one unit. Do not box every component the same way. Do not open with a project list. User Generals never explain Avalonia, Expo, or solution layout.

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

AgentUp.Browser.Streaming/
  AgentUp.Browser.Streaming.csproj

AgentUp.Capabilities.Abstractions/
  AgentUp.Capabilities.Abstractions.csproj

AgentUp.Capabilities.Common/
  AgentUp.Capabilities.Common.csproj

AgentUp.Capabilities.Dotnet/
  AgentUp.Capabilities.Dotnet.csproj

AgentUp.Capabilities.Docker/
  AgentUp.Capabilities.Docker.csproj

AgentUp.Capabilities.Codex/
  AgentUp.Capabilities.Codex.csproj

AgentUp.Capabilities.Cursor/
  AgentUp.Capabilities.Cursor.csproj

AgentUp.Capabilities.Claude/
  AgentUp.Capabilities.Claude.csproj

AgentUp.Desktop/
  AgentUp.Desktop.csproj

AgentUp.Mobile/
  package.json

AgentUp.Mobile.E2E/
  package.json

AgentUp.Mobile.E2E.App/
  package.json

AgentUp.Chat/
  package.json

AgentUp.AgentAuth/
  package.json

AgentUp.ServerClient/
  package.json

AgentUp.TestAgents/
  AgentUp.TestAgents.csproj

AgentUp.TestAgents.Tests/
  AgentUp.TestAgents.Tests.csproj

AgentUp.DesignSystem/
  package.json

AgentUp.WebAudit/
  package.json

AgentUp.CLI/
  AgentUp.CLI.csproj

AgentUp.AUDebug/
  AgentUp.AUDebug.csproj

AgentUp.CommitPolicy/
  AgentUp.CommitPolicy.csproj

AgentUp.Verification/
  AgentUp.Verification.csproj

AgentUp.Tray/
  AgentUp.Tray.csproj

AgentUp.InstallerConfig/
  AgentUp.InstallerConfig.csproj

AgentUp.InstallerApp/
  AgentUp.InstallerApp.csproj

AgentUp.Packaging/
  AgentUp.Packaging.csproj

AgentUp.PackageSmoke/
  AgentUp.PackageSmoke.csproj

AgentUp.Server.Tests/
  AgentUp.Server.Tests.csproj

AgentUp.Browser.Streaming.Tests/
  AgentUp.Browser.Streaming.Tests.csproj

AgentUp.Browser.Streaming.Benchmarks/
  AgentUp.Browser.Streaming.Benchmarks.csproj

AgentUp.Server.Benchmarks/
  AgentUp.Server.Benchmarks.csproj

AgentUp.Capabilities.Abstractions.Tests/
  AgentUp.Capabilities.Abstractions.Tests.csproj

AgentUp.Capabilities.Common.Tests/
  AgentUp.Capabilities.Common.Tests.csproj

AgentUp.Capabilities.Dotnet.Tests/
  AgentUp.Capabilities.Dotnet.Tests.csproj

AgentUp.Capabilities.Docker.Tests/
  AgentUp.Capabilities.Docker.Tests.csproj

AgentUp.Capabilities.Codex.Tests/
  AgentUp.Capabilities.Codex.Tests.csproj

AgentUp.Capabilities.Cursor.Tests/
  AgentUp.Capabilities.Cursor.Tests.csproj

AgentUp.Capabilities.Claude.Tests/
  AgentUp.Capabilities.Claude.Tests.csproj

AgentUp.Desktop.Tests/
  AgentUp.Desktop.Tests.csproj

AgentUp.CLI.Tests/
  AgentUp.CLI.Tests.csproj

AgentUp.AUDebug.Tests/
  AgentUp.AUDebug.Tests.csproj

AgentUp.CommitPolicy.Tests/
  AgentUp.CommitPolicy.Tests.csproj

AgentUp.Verification.Tests/
  AgentUp.Verification.Tests.csproj

AgentUp.Tray.Tests/
  AgentUp.Tray.Tests.csproj

AgentUp.InstallerConfig.Tests/
  AgentUp.InstallerConfig.Tests.csproj

AgentUp.Architecture.Tests/
  AgentUp.Architecture.Tests.csproj

AgentUp.Tests/
  AgentUp.Tests.csproj
```

Project directories live directly at the repository root and are included in the appropriate solution. Do not introduce `src/` or `tests/` wrapper directories unless the repository is intentionally reorganized everywhere. `agent-up.sln` references only Agent-Up product and test projects; Agent-Up projects consume LocalInstaller through `LocalInstaller.*` NuGet packages pinned by `$(LocalInstallerVersion)`. The LocalInstaller source, tests, samples, and `localinstaller.sln` live in the sibling LocalInstaller repository.

The exact project list may evolve, but ownership must not drift:

| Area | Owns |
|---|---|
| `AgentUp.Server` | Workspace registry, managed source clones, Git working-tree review and commits, the optional Git-backed dependent proposal queue and its managed worktree, process lifecycle, ports, authenticated HTTPS forwarding of allocated HTTP application ports, Docker, browser lifecycle, hosted Linux desktop application sessions, one authenticated ACP agent session per workspace, diagnostics, event recording, MCP, REST API |
| `AgentUp.Browser.Streaming` | Reusable remote-display viewer and bounded multi-subscriber frame/input transport for Server-owned graphical sessions |
| `AgentUp.Browser.Streaming.Benchmarks` | BenchmarkDotNet measurements for designated performance-sensitive browser streaming paths; runs as a receipt-backed slow verification check |
| `AgentUp.Server.Benchmarks` | BenchmarkDotNet measurements and stored regression baseline for live agent event framing |
| `AgentUp.Capabilities.Abstractions` | Stable capability adapter interfaces, manifest DTOs, installed-version inventory contracts, validation results, and launch plans |
| `AgentUp.Capabilities.Common` | Shared capability catalog parsing, checksum validation, Agent-Up tool-cache layout, install planning, capability inventory, and CLI executable discovery used by first-party and future external capabilities |
| `AgentUp.Capabilities.Dotnet` | First-party .NET ecosystem adapter, SDK discovery, version reconciliation, and `dotnet` launch planning |
| `AgentUp.Capabilities.Docker` | First-party Docker ecosystem adapter, Docker discovery, validation, and Docker launch planning |
| `AgentUp.Capabilities.Codex` | First-party Codex ACP adapter, Codex CLI discovery, validation, and ACP launch planning |
| `AgentUp.Capabilities.Cursor` | First-party Cursor ACP adapter, Cursor Agent CLI discovery, validation, and ACP launch planning |
| `AgentUp.Capabilities.Claude` | First-party Claude ACP adapter, Claude Code CLI discovery, validation, and ACP launch planning |
| `AgentUp.Desktop` | Avalonia UI, workspace display, logs, diagnostics, embedded WebView browser views |
| `AgentUp.Mobile/` | Expo and React Native client for Android, iOS, and the installable web PWA; displays Server-owned state and submits user requests |
| `AgentUp.DesignSystem/` | Canonical HTML/CSS product, documentation, and marketing design contract; generates the React Native, CommonJS, and Avalonia resource and style bindings consumed by Agent-Up surfaces and external marketing repositories |
| `AgentUp.WebAudit/` | Publishable `@agent-up/audit` TypeScript browser client for sending managed frontend audit events to the Server; owns no audit state |
| `AgentUp.CLI` | Thin human-friendly command wrapper over Server capabilities; its legacy independent local commit queue remains only for repositories that have not enabled the Server-owned Git proposal queue |
| `AgentUp.AUDebug` | Maintainer visual-debug CLI (`au-debug`) that hosts repo Desktop, Mobile, and docs together for screenshot and UI-flow inspection |
| `AgentUp.CommitPolicy` | Shared commit-message prefix, scope, and file-classification policy used by Server MCP and CLI local commit queues |
| `AgentUp.Verification` | Owns `agent-up.json`'s `verification` schema, the static path-rule check resolver, and the content-addressed receipt ledger used by the Server MCP verification tools and the `agent-up verify` CLI. Never reads the commit queue, which is what keeps the commit module optional |
| `AgentUp.Tray` | Installed Server companion that keeps a login autostart entry and heartbeats `POST /api/tray/heartbeat` so the Server knows a workstation session is present |
| `AgentUp.InstallerConfig` | Agent-Up product installer identity and repository `.env` loading used by Server, Desktop, and CLI |
| `LocalInstaller.Core` | Product-neutral installer prerequisite, component selection, PATH, validation, and uninstall planning contracts |
| `LocalInstaller.App` | Product-neutral Avalonia installer dashboard over platform installer adapters and installer-owned capability catalog state; no compile-time dependency on `AgentUp.Capabilities.*` |
| `LocalInstaller.Packaging` | Product-neutral release artifact staging, package metadata generation, and native packaging tool orchestration |
| `LocalInstaller.Smoke` | Product-neutral package and installed-service smoke validation adapters used by CI smoke scripts |
| `AgentUp.InstallerApp` | Thin Agent-Up installer entrypoint that registers the Agent-Up product manifest through LocalInstaller |
| `AgentUp.Packaging` | Thin Agent-Up packaging entrypoint that registers the Agent-Up package manifest through LocalInstaller |
| `AgentUp.PackageSmoke` | Thin Agent-Up smoke entrypoint that registers the Agent-Up smoke manifest through LocalInstaller |
| MCP clients | Automation interface; no local orchestration |

Read the full architecture guide before making structural changes: `docs/developer-guide/repo/architecture.md`.

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
    SourceClones/     (repositories Agent-Up clones and registers for itself)
      Controllers/
      DTOs/
      Interfaces/
      Providers/
      Services/
    Git/              (working-tree change tree, per-file diffs, selective commits, remotes, fetch/pull/push, commit log)
      Controllers/
      DTOs/
      Interfaces/
      Providers/
      Services/
    Browser/
      Services/
      Profiles/
      Automation/
    Ports/
      Interfaces/
      Models/
      Providers/
      Services/
    ApplicationProxy/ (authenticated HTTPS tunnel of allocated HTTP application ports)
      Controllers/
      DTOs/
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
    Git/              (Commit tab Git changes surface: change tree, file diff modal, commit box, history, proposal-queue display)
      Controllers/
      DTOs/
      Interfaces/
      Providers/
      Services/
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
    Commits/          (legacy local queue when commits.enabled is absent; otherwise a thin client of the Server proposal queue)
      Controllers/
      DTOs/
      Interfaces/
      Models/
      Providers/
      Services/

AgentUp.AUDebug/
  Features/
    Host/             (au-debug up/down/status, session, log mux, 30s readiness watchdog)
    Desktop/          (desktop screenshot, login, start-workspace)
    Mobile/           (mobile screenshot, login)
    Docs/             (docs screenshot of a hosted page, optional path/heading/full-page)
    Test/             (scoped and full visual-iteration test runs)

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
    SourceClones/
      Controller/
      Unit/
      Provider/
      HTTP/
    Git/
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
- Managed source clones and their storage root.
- Git working-tree change trees, per-file diffs, selective commits, remote-tracking branches, fetch/pull/push, and a bounded commit log.
- Process lifecycle.
- Port allocation.
- Authenticated HTTPS forwarding of allocated HTTP application ports.
- Hosted Linux desktop application sessions.
- Docker lifecycle.
- Capability reconciliation and status.
- Browser lifecycle.
- Browser profiles.
- Browser session persistence.
- Event recording.
- Diagnostics.
- Health monitoring.
- Playwright generation.
- One ACP agent session per workspace, including process lifecycle, prompts, permission decisions, and event streaming.
- MCP server.
- REST API.

No orchestration logic belongs in Desktop, CLI, or MCP clients.

The Server requires its single administrator to log in with the password from
`AGENTUP_ADMIN_PASSWORD` when authentication is enabled. REST authorization is
required by default for every route unless the endpoint explicitly opts out.
Set `AGENTUP_AUTH_DISABLED=true` only for an intentionally unauthenticated Server.
The Server starts even when `AGENTUP_ADMIN_PASSWORD` is unset; login succeeds only
after that password is configured. For local development, Server, Desktop, and
CLI load a repository-root `.env` file when present; see `.env.example`.
MCP routes remain unauthenticated and must accept connections only from a
loopback address, even when the REST listener is exposed to another subnet.

Full guide: `docs/developer-guide/workspaces/index.md`.

# Client Rules

## Desktop

The Desktop is an Avalonia client for humans. It displays workspaces, browser tabs, logs, diagnostics, health, and running processes.

Applications declared in `desktopApplications` are displayed in session-ticketed streamed application tabs. Desktop must not launch their virtual displays, capture frames, or own input/session state. Existing HTTP application tabs continue to connect directly to their allocated ports and do not use the streaming path.

It connects to one Server at a time and may remember additional Server URLs with their login tokens. Switching Servers drops Desktop-local workspace and browser state. It must not own runtime state; its Commit tab Git changes surface displays the Server-owned proposal queue, including entry order, messages, and verification state. Full guide: `docs/developer-guide/workspaces/index.md` and `docs/developer-guide/applications/index.md`.

Installed Desktop packages must install or depend on a local Server service rather than embedding orchestration in the Desktop process.

## CLI

The CLI is a thin developer convenience wrapper over Server capabilities.

It should forward commands such as restart, stop, status, and logs to the Server. User guide: `docs/user-docs/workspaces/index.md`.

## AUDebug

`AgentUp.AUDebug` (`au-debug`) is a maintainer visual-debug CLI. It hosts the repository Server, Desktop, Mobile web export, and docs site together so agents can screenshot and drive login/workspace flows without using a packaged install. It is not an orchestration owner and is not a packaged product.

Full guide: `docs/developer-guide/repo/au-debug.md`.

## Mobile

The mobile client is a single Expo and React Native TypeScript project that targets Android, iOS, and an installable web PWA. It lives in `AgentUp.Mobile/` at the repository root and is not part of `agent-up.sln`.

Mobile route entrypoints stay thin under `src/app/`; product UI and client behavior live in capability-oriented slices under `src/features/`. Do not commit Expo-generated `android/` or `ios/` projects unless native customization is intentionally adopted. The mobile client displays Server-owned state and must not own orchestration. It can save multiple Server URLs and switch among them; only one is active, and switching drops client-local workspace state.

Mobile application spaces load each application's HTTP interface in a native WebView (or web iframe). The Server reverse-proxies that traffic over the authenticated HTTPS Server origin so dynamically allocated loopback ports stay private to the Server host and are never published through the public reverse proxy. Mobile first requests a short-lived single-use ticket over Bearer REST, then navigates the WebView to the ticket bootstrap URL. Native WebViews send that ticket in the `X-Agent-Up-Ticket` header; the installable web client places it in the URL fragment so it is not logged as a query string. The Server ignores query-string tickets, sets an HttpOnly cookie, and redirects to `/` so the application is rendered at origin root. Subsequent document, asset, and WebSocket requests on unmatched Server paths use that cookie. Token-bearing ticket requests and ticket or session acceptance reject remote plaintext HTTP except for loopback development URLs. The Server decides that from the TLS connection or loopback peer, not from a client-supplied forwarded scheme header.

Mobile renders `desktopApplications` through the same session-ticketed Server viewer as Desktop: `react-native-webview` on Android/iOS and an iframe in the PWA. It must not proxy or own the display stream.

Each workspace has a local bottom bar with Apps, Git, and Agents overview tabs. The Git tab overview lists uncommitted changes and hosts Reload, Fetch, Pull, Push, and a History action. Force push is offered only after a rejected non-fast-forward push. Inner Git Review, History, application, and agent-chat pages return through the nav-bar back button. Mobile's Git Review page reads and displays the Server-owned proposal queue. It must not reconstruct queue ancestry or infer verification state locally.

Agent sign-in on every client goes through `AgentUp.AgentAuth` (`@agent-up/agent-auth`). It branches on the transport the Server reports - `poll`, `code`, or `redirect` - and never on which agent is signing in, so the real Claude, Codex, and Cursor CLIs and the test agents drive one code path rather than parallel ones. Platform behavior lives in an adapter behind a port; the state machine stays free of React and React Native imports so it is tested under plain Node.

The `redirect` transport must open an in-app WebView, not the system browser. The agent CLI's callback is bound to loopback on the Server host, so a client only completes that sign-in by observing the navigation and posting it to `POST agent/login/callback`; `Linking.openURL` hands the URL to Safari or Chrome and nothing comes back. A client must not open a sign-in link the user did not ask it to open.

Developer guides: `docs/developer-guide/workspaces/index.md`, and `docs/developer-guide/agents/sign-in.md` for the sign-in transports.

## MCP

MCP is the primary automation interface for AI agents.

Agents should use MCP directly instead of shelling through the CLI when browser inspection, interaction, diagnostics, logs, screenshots, or Playwright generation are needed. Full guide: `docs/developer-guide/index.md` and the slice Generals.

Agent-Up MCP initialization instructions must tell clients to use `start_workspace` immediately when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up; this means starting the local managed development environment, not deploying to cloud infrastructure. Agents should not call `list_workspaces` or `get_workspace_status` first when the current repository/worktree is known.

# Configuration Rules

Every managed repository is described declaratively with `agent-up.json`.

Managed applications must not reference Agent-Up packages, SDKs, or APIs. Agent-Up injects runtime values through environment variables and process launch configuration. The first-party AgentUp.Mobile client is an explicit exception and may consume the state-free `@agent-up/audit` browser transport because it is itself an Agent-Up product client.

Legacy local application commands and legacy Docker `services` remain supported. Local application commands are executable-plus-arguments strings, not shell expressions; the Server launches them directly with an argument list and rejects shell chaining, redirects, variable expansion, and subshells. New ecosystem-aware configuration should prefer capability sections such as `dotnet` and `docker`; the Server reconciles declared version requirements with versions discovered or managed by capability adapters, then exposes capability status to Desktop, CLI, and automation clients.

The optional `commits.enabled` setting opts a repository into the Server-owned Git proposal queue. Its entries form a linear dependent stack in a private managed worktree and are stored as commits under Agent-Up namespaced refs without moving or committing the developer's branch. Enqueue runs the Verification rules for the proposed delta before recording the entry. Agents must continue dependent work at the managed queue worktree path returned by enqueue. Omitting the setting preserves the legacy independent patch queue during migration.

A local application entry may declare `install`, an executable-plus-arguments command (same allowlist and shell rejection as `command`) run to completion in the application's `path` before every launch of `command`. It has no separate "already installed" tracking: the Server reruns it on every start and restart and relies on the command itself being idempotent (`npm install`, `dotnet restore`, `pip install -r requirements.txt`). Output streams to the application console prefixed with `[install]`; a non-zero exit fails the start without launching `command`.

The root `desktopApplications` collection declares Linux GUI processes. Each entry follows the local application command, install, path, environment, environment-file, and port rules, uses the allowlisted `linux` runtime, and may additionally set a fixed `window.width` and `window.height`. The Server creates an isolated Xvfb display before launch, injects `DISPLAY` plus a private `XDG_RUNTIME_DIR`, X11-only toolkit variables (`GDK_BACKEND=x11`, `WAYLAND_DISPLAY=agentup-hosted-no-wayland`, `XDG_SESSION_TYPE=x11`), and a native library path so SkiaSharp and GUI toolkits can load when the Server itself was not started inside `nix-shell`. The process cannot attach to the workstation session. The Server owns framebuffer capture and input, and issues session-scoped viewer tickets to Desktop and Mobile. Desktop application validation uses generation-scoped framebuffer coordinates; stale-generation input is rejected.

The optional root `display` object in `agent-up.json` is only for Desktop visuals. `display.name` overrides the workspace entry title and `display.branch` overrides the workspace entry subtitle. These values must not change repository path identity, worktree path handling, Git branch detection, commit identity, audit identity, or process working directories.

User docs:

- `docs/user-docs/configuration/index.md`
- `docs/user-docs/configuration/reference.md`

# Port Allocation

The Server owns all ports.

Each workspace receives a dedicated contiguous port range. Applications consume only environment variables such as `WEB_PORT`, `API_PORT`, and `AUTH_PORT`.

Workspace guide: `docs/user-docs/workspaces/index.md`.

# Browser Model

Each workspace owns separate Desktop and Server headless browser profiles.

The Server manages headless browser lifecycle and stores automation state under `browser-profiles/{workspaceId}`. Desktop creates independent `NativeWebView` instances for HTTP port tabs. These surfaces do not share cookies, local storage, session storage, IndexedDB, cache, or navigation state.

User docs:

- `docs/user-docs/browser/index.md`
- `docs/user-docs/browser/validation.md`

# Browser Automation

AI agents interact with applications through Server-backed browser automation.

Prefer structured inspection and accessibility data over raw HTML. Every interaction should be recordable as an event that can later support diagnostics, workflow inference, and Playwright generation. Validation flows describe the user-meaningful route through the GUI and the visible expectations at each step, not implementation details; the Server persists versioned flows that agents can edit, re-record, replay in the Desktop WebView with staged mouse movement and attention pings, and export as port-independent Playwright tests for headless CI.

Developer guides:

- `docs/developer-guide/diagnostics/events.md`
- `docs/developer-guide/browser/validation.md`

# Diagnostics

Diagnostics are collected continuously by the Server and exposed to Desktop, CLI, and MCP clients.

Diagnostics include console output, JavaScript exceptions, failed network requests, performance timings, health information, and process status.

Managed application processes receive `AGENT_UP_AUDIT_ENDPOINT`,
`AGENT_UP_WORKSPACE_ID`, and `AGENT_UP_APPLICATION`. Frontend builds may expose
those values to `@agent-up/audit`; the endpoint must identify the orchestrating
Server's configured URL rather than assuming a fixed development or packaged port.
The Server owns ingestion, identity enrichment,
storage, and paginated per-application queries. Desktop renders that audit trail
with native Avalonia controls next to each application's Console tab.

Product crash reporting for Server, Desktop, CLI, and Mobile is separate from
workspace diagnostics. See `docs/developer-guide/repo/telemetry.md`.

Full guide: `docs/developer-guide/diagnostics/index.md`.

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
| `AgentUp.Browser.Streaming` | `AgentUp.Browser.Streaming.Tests` |
| `AgentUp.Capabilities.Abstractions` | `AgentUp.Capabilities.Abstractions.Tests` |
| `AgentUp.Capabilities.Common` | `AgentUp.Capabilities.Common.Tests` |
| `AgentUp.Capabilities.Dotnet` | `AgentUp.Capabilities.Dotnet.Tests` |
| `AgentUp.Capabilities.Docker` | `AgentUp.Capabilities.Docker.Tests` |
| `AgentUp.Capabilities.Codex` | `AgentUp.Capabilities.Codex.Tests` |
| `AgentUp.Capabilities.Cursor` | `AgentUp.Capabilities.Cursor.Tests` |
| `AgentUp.Capabilities.Claude` | `AgentUp.Capabilities.Claude.Tests` |
| `AgentUp.Desktop` | `AgentUp.Desktop.Tests` |
| `AgentUp.CLI` | `AgentUp.CLI.Tests` |
| `AgentUp.AUDebug` | `AgentUp.AUDebug.Tests` |
| `AgentUp.Verification` | `AgentUp.Verification.Tests` |
| `AgentUp.CommitPolicy` | `AgentUp.CommitPolicy.Tests` |
| `AgentUp.Tray` | `AgentUp.Tray.Tests` |
| `AgentUp.InstallerConfig` | `AgentUp.InstallerConfig.Tests` |
| `AgentUp.TestAgents` | `AgentUp.TestAgents.Tests` |
| `LocalInstaller.Core` | `LocalInstaller.Core.Tests` |
| `LocalInstaller.App` | `LocalInstaller.App.Tests` |
| `LocalInstaller.Packaging` | `LocalInstaller.Packaging.Tests` |
| `LocalInstaller.Smoke` | `LocalInstaller.Smoke.Tests` |

`AgentUp.Architecture.Tests` is a dedicated ArchUnitNET/NUnit project for executable architecture and review-hygiene rules over source owned by the Agent-Up repository. It validates production project dependency ownership, feature/type-folder layout, shared-folder layout, concrete controller boundary presence for slices with inbound traffic, controller dependency construction rules, controller separation from providers/repositories/factories, controller and service sibling-slice boundary usage, controller method complexity, nested production type bans, feature test-kind coverage, error-handling hygiene, path/disposable/async safety, test taxonomy rules, MCP endpoint tool-allowlist completeness, and verification path-rule coverage for every production and test project. LocalInstaller source architecture is tested in the sibling LocalInstaller repository. Keep architecture and generic source hygiene rules in the owning repository instead of burying them in product E2E tests.

`AgentUp.Tests` is a separate cross-product E2E project that exercises the full Desktop application and shared Installer application through platform fixture adapters. Desktop browser behavior that only exists once a real WebView engine and a real platform storage provider are involved — the WebView upload bridge and OAuth sign-in through redirects and native popups — belongs here rather than in headless tests, and must substitute only the modal dialogs and native engine callbacks a CI runner cannot drive. Linux uses `AgentUp.Fixtures.Linux` with Xvfb and WebKitGTK: the adapter always starts a private Xvfb, `XDG_RUNTIME_DIR`, and session bus, imports `PATH` and native libraries from `nix-shell shell.nix` so IDEs do not need extra env vars, and Avalonia is forced onto X11, so a developer workstation's Wayland/X11 session is not reused. Set `AGENTUP_E2E_USE_SESSION_DISPLAY=1` only to debug against the real display. macOS uses `AgentUp.Fixtures.MacOs` and isolates test home and temporary storage. Windows uses `AgentUp.Fixtures.Windows` and isolates WebView2 profile storage through `WEBVIEW2_USER_DATA_FOLDER` only: it must leave `LOCALAPPDATA` and `APPDATA` pointing at the real profile, because redirecting them isolates nothing Desktop stores -- `Environment.GetFolderPath` asks the shell rather than the environment on Windows -- while the WebView2 browser process does inherit them and never finishes starting from an empty profile root, which hangs the UI thread before any test runs. Isolating Desktop's own storage on Windows needs a storage-root override in the product, which does not exist yet. Platform-specific canary tests enforce both isolation contracts; successful native Avalonia/WebView startup proves the hosted desktop session itself. Both start Avalonia against the native desktop/WebView backend available on the CI runner. These tests are part of the normal platform test run: Ubuntu runs them in the GUI test job, while the macOS and Windows package jobs execute a framework-dependent test artifact produced by the Ubuntu payload job against the matching .NET runtime those jobs install. macOS CI runs the project through its NUnitLite executable entry point so Avalonia Native initializes on the process main thread while still exercising the same test fixtures and native WebView.

Changes to packaging, installers, CI payload staging, Desktop startup, browser/WebView hosting, or installed app layout that can affect the delivered Desktop or InstallerApp runtime must run the relevant project tests and `AgentUp.Tests` in the same verification pass. Do not claim completion for those changes after only running the package, installer, or app unit test projects.

After every task that touches any production project, run the architecture tests before reporting completion:

```
./au-debug test architecture
```

All architecture rules must pass. Fix any violation before considering the task done. Do not move on, commit, or report success while architecture tests are failing.

`AgentUp.Mobile.E2E` is the mobile end-to-end project. It drives agent sign-in on a real iOS simulator (Detox, `macos-latest`), a real Android emulator (Detox, `ubuntu-latest` with KVM), and the installable web export (Playwright), against a real Agent-Up Server process, the real test agent CLIs from `AgentUp.TestAgents`, and the real identity provider behind them. Nothing in it is in-process, headless-only, or faked.

The app it drives is `AgentUp.Mobile.E2E.App`: the real `AgentUp.Chat` and `AgentUp.AgentAuth` modules mounted with nothing around them. It exists so the suite tests sign-in rather than navigation - reaching the chat in the full client took four taps through the sidebar, a workspace list and a dashboard, every one of them a way for an unrelated change to fail this suite - and so the app it compiles is small enough to build often. The code under test is the same module the shipping client mounts; only the shell around it is missing.

`AgentUp.Chat` is that module: the transcript, the permission prompts and the subscription sign-in, with no import from any app. Which workspace, which Server, and an optional Git changes panel all arrive as props, which is what lets the client and the harness run one implementation instead of two. Shipping Mobile mounts the chat without that panel. It reaches a Server through `AgentUp.ServerClient`, the transport the client's own slices use.

The disposable native sign-in harness permits cleartext traffic only so its Android emulator and iOS simulator can reach ephemeral Server and identity-provider processes on the CI host. Production Mobile transport policy must not inherit that exception.

Both modules are consumed as `file:` dependencies and ship TypeScript sources, so every app that mounts them needs the `metro.config.js` dedupe they come with: Metro resolves a symlinked package's imports from its own `node_modules` first, and a second copy of `react` there means the module's hooks read a different dispatcher than the app rendered with and throw on mount. `AgentUp.Mobile.E2E/pwa/mounts.spec.mjs` is what catches that, because it happened.

`plugins/withoutReleaseLint.js` turns off lint's release checks there. `assembleRelease` runs lintVital, which reads every proguard file the variant declares and, on a hosted runner, walks into `/home/packer` - the image builder's home directory, not readable by the runner - so the task cannot succeed. This app is never shipped, so lint has nothing to protect in it; the real client keeps its own lint untouched.

Its androidTest manifest comes from `AgentUp.Mobile.E2E.App/plugins/withDetoxAndroidTestManifest.js`. androidx.test's `InstrumentationActivityInvoker` declares three activities with intent filters and no `android:exported`, which anything targeting Android 12 or higher must state explicitly, so the merge fails and the test APK is never assembled. The androidTest source set's manifest is the highest-priority one for that APK, so stating it there settles the merge whatever version of androidx.test is resolved - and the version arrives transitively through Detox, so it is not ours to pin.

Detox synchronises on the app being idle, and the chat holds an event stream open for its whole life, so `detox/signIn.test.js` excludes that stream from synchronisation with `device.setURLBlacklist`. Without it the tap that mounts the chat never reports back and every native scenario dies on the hook timeout: the action reaches the app, the app opens the stream, and Detox waits for an idle that cannot come. Nothing else relies on that heuristic here - every wait in these suites is a condition on Server state or on an element being visible.

The harness app carries `@config-plugins/detox`, and Android does not run without it. `expo prebuild` generates a plain Android project with no instrumentation: no `androidTest` source set, no `testInstrumentationRunner`, no Detox dependency - so `assembleAndroidTest` produces nothing and every run dies with "Failed to find the app binary". The plugin injects exactly those, plus a maven repo pointing at the npm-pinned copy of Detox so `com.wix:detox:+` resolves to the version the lockfile names. Its peer range still says expo ^53, which is stale metadata rather than a real incompatibility, so the app's `.npmrc` sets `legacy-peer-deps`; what it generates is verified by prebuilding and reading the project, not assumed.

Sign-in runs in its own workflow, `.github/workflows/mobile-agent-auth-ci.yml`, scoped by path to the things it tests. That filter is the whole correctness argument for the gate, and it includes `AgentUp.Server` and `AgentUp.TestAgents` alongside the client modules: the Server drives every one of these sign-ins and the test agents implement them, so a change to either is exactly what this suite exists to catch. A nightly run covers whatever the filter misses.

`AgentUp.TestAgents` publishes one binary launched through a per-agent shim: `test-agent1` (loopback redirect, the `codex login` shape), `test-agent2` (device code, `codex login --device-auth`), `test-agent3` (pasted code with an unterminated prompt, `claude setup-token`), `test-agent4` (silent polling, `cursor-agent login`), and `test-idp`. Each speaks real ACP v1 over stdio and refuses `session/new` until it holds a credential. There are four sign-in shapes but only three agent kinds, so the loopback-redirect and device-code agents share the Codex slot and the stack starts twice rather than letting them collide.

Every process the harness starts is watched by `AgentUp.Mobile.E2E/harness/supervise.mjs`, because a stack that fails to come up has to say why. Without it a process that dies on startup looks exactly like a slow one: the wait runs its full minute and reports `fetch failed`, which is true and useless - and that is precisely how one run lost the identity provider and left nothing behind to explain it. What each process said is kept and reported, and its death ends the wait at once instead of a minute later.

These tests must not be flaky, and that is enforced rather than hoped for. No fixed delays: `AgentUp.Mobile.E2E/scripts/forbid-sleep.mjs` fails the build on `setTimeout`, `device.sleep`, or `page.waitForTimeout` outside the wait helper, and every wait is a condition plus a deadline that names what it was waiting for. Approval is a control-plane call to the test identity provider at a moment the test chooses, never a wait on a polling interval and never a click driven into a browser's DOM. Ports are ephemeral, versions are pinned (simulator runtime, system image, API level, Detox, Playwright), app state is reset per case, and there are no retries: a retry hides a flake instead of surfacing it.

The mobile CI jobs sit at the same dependency tier as `docs` and `jetbrains-plugin`, and each publishes the Server and test agents itself instead of taking an artifact from the .NET chain. That publish runs while the job is already provisioning an emulator or an Xcode toolchain; an artifact dependency would serialise the mobile suite behind the slowest jobs in the pipeline.

The three end-to-end jobs hold a runner for tens of minutes, one of them macOS. They run on every push regardless: a suite that decides for itself when to run cannot be used to gain confidence in a change, because a green pipeline stops distinguishing "passed" from "never ran". What bounds them instead is the cache - an unchanged client reuses the app it already built - and a per-job `concurrency` group with `cancel-in-progress`, because the workflow-level group deliberately never cancels a branch run and without that a full set survived every push until the runners were saturated.

`AgentUp.AgentAuth/dist` is committed, as `AgentUp.WebAudit/dist` and `AgentUp.DesignSystem/dist` already are: Cloudflare Pages builds the web client through `build:cloudflare` in its own Node image and cannot build sibling packages first. The `mobile` job rebuilds it and fails on any diff, so the committed output cannot drift from its source.

Expo generates `AgentUp.Mobile/ios/` and `AgentUp.Mobile/android/` during those jobs and they stay uncommitted. `.github/scripts/install-mobile-deps.sh` installs mobile dependencies for CI without the `nix-shell` wrapper the repository npm scripts use: those runners have no Nix and do have Xcode and Android toolchains that the wrapper's replaced `PATH` would break. It runs the same underlying commands; only the shell wrapper is skipped. This is the second documented exception alongside `build:cloudflare`.

Changes under `AgentUp.Mobile/` must run `./au-debug test mobile` (typecheck, tests, and web export). Add focused client tests with new behavior once the corresponding test boundary exists; a static export alone must not substitute for behavior tests.

Every public mobile npm script must invoke its Expo or TypeScript command through the repository `shell.nix`, except `build:cloudflare`, which runs the shared web-export entrypoint directly in Cloudflare Pages' Node.js build image. Do not add other duplicate direct or `:nix` script variants. Expo commands must use the local `node_modules/.bin` CLI, and TypeScript commands must use `npx`; `nix-shell` replaces `PATH`, so a bare `expo` or `tsc` binary is not available. Keep Node.js, `NIX_LD`, `patchelf`, the DotSlash DevTools preparation, and the React Native DevTools Electron runtime libraries in `shell.nix` so NixOS launches use the same reproducible environment.

Mobile development servers use Expo LAN mode so Metro is reachable through the host network. Production web builds must export through Metro. Keep the web manifest and install icons under `public/` synchronized with the exported PWA.

Forbidden:

- Changing production behavior without updating or adding tests for that behavior.
- Adding REST endpoints or MCP tools without tests for the new contract.
- Changing request/response/resource shapes without updating tests.
- Removing behavior without removing or updating tests that covered it.
- Claiming completion while relevant tests are missing, skipped, or known broken.
- Claiming completion for packaging, installer, Desktop, browser/WebView, or installed-layout changes without running the native-display `AgentUp.Tests` project unless the platform lacks the required native display dependencies; in that case, report the missing dependency and the exact CI-shaped command that still needs to run.

## Test Structure

Tests should follow the same feature/slice layout as production code.

Architecture rules belong in `AgentUp.Architecture.Tests`. Use ArchUnitNET for assembly/type dependency rules and focused filesystem/source checks for physical layout rules ArchUnitNET cannot observe. Root-level test support folders are limited to documented support areas such as `Support/`, `Fixtures/`, `Fake/`, `Architecture/`, or root `E2E/`; test-kind folders such as `Controller/` and `Benchmark/` must stay under `Features/<Slice>/`.

Feature slices with `Controllers/`, `Services/` or `Models/`, and `Providers/` should have matching `Controller/`, `Unit/`, and `Provider/` test-kind coverage. Existing gaps are tracked as explicit architecture-test debt; new or expanded slices must not add to that baseline.

Every production project owns a test project of the same name plus `.Tests`, so a change to
it selects one suite rather than being covered incidentally by another project's tests. The
exception is `AgentUp.InstallerApp`, `AgentUp.Packaging` and `AgentUp.PackageSmoke`: each is
a `Program.cs` handing manifests to a LocalInstaller builder, with nothing to assert but the
builder chain itself. `ArchitectureFixture.CompositionOnlyProjects` names them and
`EntryPointProjects` holds them to that shape - a file with logic in one of them fails the
rule, so the code moves to a tested project or the project gains a test project and joins
`ProductionProjects`.

A type does not get tested from another project's suite because that suite happens to
reference it. `RepositoryDotEnv` lived in `AgentUp.InstallerConfig` and was tested from
`AgentUp.Server.Tests/Features/Authentication/`, which left its parsing rules almost
entirely unexercised and meant a change to it selected no suite that was actually about it.
Tests belong with the project that owns the type.

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
- Provider tests verify low-level external behavior in isolation, including filesystem providers, command/tool providers, environment providers, platform adapters, package writers/stagers, probes, generated directory state, and process-style command shapes. Temp directories are allowed when the provider boundary requires them. Codex, Cursor, and Claude Provider smoke tests discover the ACP CLIs declared in capability inventory and present on the machine, and assert both the installed and missing outcomes; Server Agents HTTP smoke uses those same live adapters and asserts the workspace agent picker matches that discovery. These tests must not skip based on whether a CLI is present.
- Performance gates cover only repeated hot paths with stored numeric baselines: streamed pointer-input decoding, live agent-event framing, and Mobile transcript/Git-tree projection. .NET gates compare BenchmarkDotNet mean time and allocation against versioned baselines; Mobile compares trimmed timing samples against its versioned baseline. A gate fails above its declared relative tolerance. Exact-file path rules select the relevant `slow` check, and CI runs every gate. Do not add one-shot, constant-return, OS-counter, no-op-provider, or external-I/O microbenchmarks; profile or soak-test those workloads instead.
- Headless tests verify Avalonia UI behavior without native display dependencies.
- End-to-end workspace lifecycle tests should be few and prove full integration across Server, process management, ports, diagnostics, and browser state.

`Unit/` tests must not use real filesystem, process execution, sockets, current-directory mutation, or environment mutation APIs. If a test needs `File.*`, `Directory.*`, `Path.GetTempPath`, `Process.Start`, `ProcessStartInfo`, `Directory.SetCurrentDirectory`, `Environment.SetEnvironmentVariable`, `TcpListener`, `TcpClient`, or `Socket`, put it in `Repository/`, `Provider/`, `HTTP/`, `Headless/`, or `E2E/` according to the behavior being observed.

Avoid duplicate tests that assert the same rule through multiple layers.

NUnit tests default to a 30-second per-test timeout from `coverlet.runsettings`. Tests that must run longer, such as capability CLI smoke and native-display E2E, set `[Timeout]` / `[CancelAfter]` on the fixture or method. A 1-minute testhost hang dump aborts a stuck session so a single hung test cannot run forever; it is not a 1-minute budget for a full project run.

CI additionally enforces Linux Release wall-clock budgets in an independent watchdog job: 60 seconds for the combined Server/Desktop `Unit` tier, 75 seconds for `Provider`, and 180 seconds for cross-product `E2E`. The watchdog gives a background Chromium installation at most 120 seconds, runs display-dependent Provider and E2E tests under Xvfb, rejects filters that execute zero tests, and uploads TRX diagnostics. Update a budget only from a recorded CI baseline and explain the changed workload; never raise it merely to make a regression green.

# Verification

The JSON field contract lives in `docs/user-docs/configuration/reference.md`. The MCP and CLI surface summary lives in `docs/developer-guide/verification/index.md`.

Test selection is not an agent decision. The `verification` section of `agent-up.json` maps
changed paths to named checks through static glob rules, and the runtime resolves them; an
agent can see the required set but cannot narrow it.

Use the MCP verification tools on the `/mcp/verification` server:

| Tool | When |
|---|---|
| `plan_verification` | See which checks the current changes require, and which rule selected each |
| `run_verification` | At the end of a task, before enqueueing commits. Runs every required check and records a receipt per check |
| `run_verification_check` | Re-run one check by id after a targeted fix |
| `guard_verification` | Report whether every required check has a passing receipt matching the current file contents |

`agent-up verify coverage` has no MCP tool of its own: it runs as the `patch-coverage`
check inside `run_verification`, so an agent gets it automatically rather than choosing it.

Developers use the CLI equivalents: `agent-up verify plan`, `agent-up verify run [<check-id>]`,
and `agent-up verify guard [--run] [--format hook]`.

## Receipts

A receipt records the command, the exit code, and the content hash of every changed file
that check covered. The guard recomputes those hashes and requires an exact match, so
running the checks and then continuing to edit leaves the receipt **stale** rather than
satisfying. Receipts live in `.git/agent-up/verification/receipts.json` - outside the
working tree, because a committed receipt would travel in a pull request and satisfy
another machine's guard against bytes it never tested.

Order matters: **run verification before enqueueing commits.** `enqueue_commit` restores
tracked files to their pre-change state, so a receipt produced afterwards would cover a
working tree that no longer holds the change.

## Running the guard at the end of a run

`verify guard` executes nothing - it hashes changed files and reads the receipt ledger - so
it is cheap enough to wire into a client-side `Stop` hook:

```json
"hooks": {
  "Stop": [
    {
      "matcher": "",
      "hooks": [
        { "type": "command", "command": "agent-up verify guard --format hook" }
      ]
    }
  ]
}
```

The hook is silent when every required check is proven, and exits 2 with the unproven
checks on stderr otherwise. `--run` additionally executes what is missing; that is
deliberately opt-in, because running suites inside a Stop hook makes every turn end slow
and turns a hook timeout into a false failure.

## Patch coverage

Every change to mapped production code must cover at least the percentage in
`coverage.minimum` (currently 90%) **of the lines it changed**. Total coverage is not
gated: on a codebase this size it barely moves per change, while patch coverage moves
immediately.

`agent-up verify coverage [--min N]` measures it. The gate reads the Cobertura reports a
test run wrote - it does not run tests itself - so the suites must collect coverage first.
The `verification.checks` test commands already do, writing into
`artifacts/coverage/<check>`, and `patch-coverage` carries `order: 100` so it runs after
them.

What counts:

- Only lines the change **added or modified**. Deleted lines require no coverage.
- Only lines a coverage tool reports as **coverable**. A blank line, a brace, or a
  declaration with no sequence point is neither covered nor a failure, so the ratio does
  not depend on formatting.
- A whole untracked file counts as added, so a brand-new file cannot score as covered by
  having no diff.
- A line hit by **any** suite counts, because reports from every test project are merged.
- `coverage.include` decides what the gate is about; `coverage.exclude` removes files where
  a coverage number carries no information. Both are globs in `agent-up.json`, reviewable
  in a diff. `AgentUp.Architecture.Tests` requires every production project to appear in
  `include` and rejects exclusions broad enough to switch the gate off.

A changed file that no report mentions blocks **only** when its whole project is missing
from every report, which means that suite never ran. A file whose project is covered but
which has no entry of its own simply has no executable code - a changed interface or enum
must not fail the gate.

Within one check only the newest run is read. The test runner adds a GUID folder per run
and never removes the previous one, so a check run twice leaves two reports; the older one
numbers the file as it was before the edit, and merging it in would report lines that have
since moved as uncovered. Across checks that protection does not apply, so every check that
collects coverage has to be current - which is what `verification.always` and receipt
staleness already guarantee. Re-running one suite by hand and then the gate does not: run
the plan, not a single check.

What `coverage.exclude` is for, and what it is not: a file belongs there when a coverage
number about it carries no information - an entry point, generated or composition-only
code, or a body that is nothing but a platform call which cannot be made on another host.
`AgentUp.Tray` shows the intended shape: the Windows Run-key *format* rules and the macOS
plist and load/unload *sequence* are injected and fully covered, while the two files that
do nothing but call the platform (`WindowsAutoStartRegistrar.cs` reaching the registry,
`LaunchctlProcess.cs` starting launchctl) are excluded by name. Split the decidable part
out and cover it; never exclude a file to avoid writing a test.

`codecov.yml` sets the same 90% patch target, and its `ignore` list must contain every
`coverage.exclude` glob - `AgentUp.Architecture.Tests` enforces that, because a glob missing
there fails a pull request the local gate passed, on lines this repository has already
decided carry no information. The two numbers are still not identical: Codecov counts
partially-covered branches, and this gate counts lines, so Codecov can read a little lower.
It is complementary, not a substitute: Codecov cannot gate a local run.

## Per-slice coverage

`agent-up verify slices [--min N]` reports total line coverage for every feature slice,
worst first, and fails a slice below `coverage.sliceMinimum`. Patch coverage keeps each
change honest but says nothing about a slice that was thin before the gate existed; this is
where that debt is visible.

The floor is lower than the patch minimum on purpose. Patch coverage governs new work at
90%; the slice floor is a line under what already exists, and raising it is a decision to
burn the remainder down.

`coverage.sliceExemptions` lists the slices allowed below the floor, each as exactly
`<Project>/Features/<Slice>` - no globs, no type folders, nothing that could silently
exempt a slice nobody reviewed. Every entry is accepted debt, and the check **fails** once a
listed slice reaches the floor, so the list cannot outlive what it records. The architecture
suite additionally rejects an entry naming a slice that no longer exists.

The check is `ciOnly`, and not out of convenience: the architecture suite instruments every
production assembly and records no hits for code it never executes, so on a dev machine,
where only the suites a change selects have run, a slice whose own suite was not selected
reads as uncovered. Run it by hand after a full sweep - `agent-up verify run` then
`agent-up verify slices` - and let CI enforce it.

`AgentUp.Architecture.Tests/Rules/SliceTestCoverage.cs` is the structural half: tests exist
in the matching test-kind folder, and more than one of them. It cannot measure coverage,
because it runs before the suites that produce the reports. Its two baselines under
`Baselines/` are ratchets - an entry that is already satisfied fails the suite, so burning
debt down means deleting the line.

## Configuration

`verification.checks` defines named checks; `verification.paths` maps globs to check ids in
order, and every match contributes. `verification.always` lists checks required for any
change at all, in the order they should run. A check declares a `tier` (`fast`, `slow`,
`platform`), optional `platforms`, optional `ciOnly`, optional `order`, and `inputs` - the
dependency closure whose changed files belong in its covered map. `order` sorts selected
checks ascending before id, which is how a check that consumes another's output is made to
follow it.

Rules to keep:

- Every production and test project must be reachable by a path rule. `AgentUp.Architecture.Tests`
  enforces this, so a new project cannot be invisible to the gate.
- A path that genuinely requires nothing declares `"checks": []`. A changed file matching no
  rule is a hard failure meaning the map is incomplete, not a pass.
- A malformed `verification` section, an unknown tier or platform, and a dangling check id all
  throw. A gate that fails open is not a gate.
- `enforcement` is `warn` or `block`. Keep a repository on `warn` while its path map is being
  completed, then switch to `block`.
- Checks whose `platforms` exclude this machine, or that are `ciOnly`, are reported *skipped
  with a reason* and never counted as satisfied. They stay required where they can run.

The commit queue owns no test metadata. Queue entries carry a slice, message, and files;
receipts are the only record of what was proven.

# Content Sections

The sections below intentionally introduce each concept briefly and point to the canonical docs page. Keep AGENTS.md concise; detailed specifications belong in `docs/`.

## Workspaces

A workspace is the unit of isolation for an agent or developer session. It is identified by project path and may include repository/worktree metadata, branch, commit, browser profile, Docker infrastructure, running processes, allocated ports, diagnostics, and event history. Non-Git project paths are valid and should display as `not on a git branch`.

Workspaces may also be created by Agent-Up itself. The Server's `SourceClones` slice clones a repository at a branch into a Server-owned source clones root (`AGENTUP_SOURCE_CLONES_ROOT`, otherwise `sources` under the data directory) and registers the result, so Desktop and Mobile both list it. Only `http`, `https`, `ssh`, and `git` remotes plus the `user@host:path` form are accepted; `file://` and transport-helper remotes are rejected so a REST caller cannot make the Server read arbitrary local repositories.

Read: `docs/user-docs/workspaces/index.md` and `docs/developer-guide/workspaces/index.md`.

## Applications

Managed applications are local processes, Docker services, and hosted Linux GUI processes. They consume allocated ports through environment variables, not hardcoded localhost values. Console, metrics, and the database explorer are application subfeatures.

Read: `docs/user-docs/applications/index.md` and `docs/developer-guide/applications/index.md`.

## Git

The Server's `Git` slice exposes the selected workspace's uncommitted changes as a directory tree, per-file diffs, a commit that stages only the requested paths, remote-tracking branches, fetch/pull/push, and a bounded commit log. It is the human review-and-commit surface rendered by the Desktop Commit tab and the Mobile Git tab, and it is deliberately separate from the `Commits` slice, which owns the agent-facing commit queue described under Commit Workflow.

Coding agents must still use the commit queue tools. The `Git` slice is a product surface for humans, not an escape hatch around `enqueue_commit`.

Read: `docs/user-docs/git/index.md` and `docs/developer-guide/git/index.md`.

## Commits

The agent queue is the **commit queue** (legacy local) or **proposal queue** when `commits.enabled` is true. Desktop Commit and Mobile Review display that queue; they do not mutate it. Agents enqueue through MCP.

Read: `docs/user-docs/commits/index.md` and `docs/developer-guide/commits/index.md`.

## Agents

Each workspace has one ACP agent session. Desktop chrome for the live session is the **Agent** tab; Mobile chrome for the picker is the **Agents** tab. Subscription sign-in is Server-owned.

Read: `docs/user-docs/agents/index.md` and `docs/developer-guide/agents/index.md`.

## Browser

Agent-Up keeps browser sessions tied to workspaces. Developers use Desktop or Mobile WebViews; agents use the Server headless profile. Those surfaces do not share cookies, storage, or navigation state. Restarting applications should reload the existing surface for that workspace rather than create more tabs.

Read: `docs/user-docs/browser/index.md` and `docs/developer-guide/browser/index.md`.

## Diagnostics

Diagnostics make AI validation practical by exposing process, browser, network, console, health, and performance information from the live workspace.

Read: `docs/user-docs/diagnostics/index.md` and `docs/developer-guide/diagnostics/index.md`.

## Verification

Path-rule checks, receipts, and coverage stay in Verification. Verification never reads the commit queue.

Read: `docs/user-docs/verification/index.md` and `docs/developer-guide/verification/index.md`.

## Configuration

Agent-Up uses declarative repository configuration through `agent-up.json`. Applications declare launch commands, port environment variables, browser paths, and Docker setup without source-code integration.

Capability sections such as `dotnet` and `docker` are the preferred shape for ecosystem-aware requirements. Capability adapters discover system and Agent-Up-managed versions, reconcile declared requirements, return structured mismatch status, and produce Server-owned launch plans. The legacy `applications` list remains supported for executable-plus-arguments commands, and legacy Docker `services` remain supported for compatibility.

Read: `docs/user-docs/configuration/index.md` and `docs/developer-guide/configuration/index.md`.

## AUDebug

`au-debug` hosts repo Desktop, Mobile, and docs for visual comparison. One-shot commands use a 30 second watchdog. Probe the host with `au-debug status` instead of curling ports or searching windows. Capture a hosted docs page with `au-debug docs screenshot [path]`; add `--heading <text>` to scroll that heading into view, or `--full-page` to capture the whole document. Run visual-iteration checks with `au-debug test <suite>` or `au-debug test`. Rebuild generated design-system bindings with `au-debug build design-system`, and Mobile typecheck plus web export with `au-debug build mobile`. Do not invoke those npm or `dotnet test` commands directly when an `au-debug` wrap exists. Read: `docs/developer-guide/repo/au-debug.md`.

MCP is a protocol, not a slice. Attach to `/mcp/orchestration`, `/mcp/browser`, `/mcp/audit`, `/mcp/commits`, and `/mcp/verification`. Each developer General lists that slice's tools. See `docs/developer-guide/index.md`.

## Product telemetry

Agent-Up product processes report their own crashes to self-hosted Sentry.
Workspace and application diagnostics stay in Server audit. Unset DSN is a
no-op. Do not mint Sentry tokens at runtime. Cluster Helm DSNs are written
by the GitOps sentry-configurator Job, not by the Sentry UI.

Read: `docs/developer-guide/repo/telemetry.md`.

## Workflows

The target AI workflow is: modify code, restart workspace, wait until healthy, inspect page, interact, validate, screenshot, generate Playwright, commit.

Read: `docs/developer-guide/workspaces/workflows.md`.

## Commit Workflow

Coding agents must not run `git commit`, `git add`, or `git stash` directly. Instead, use the MCP `enqueue_commit` tool to declare each vertical-slice commit at the end of a task. Use `enqueue_review_fix_commit` when fixing a pull request review issue; each queued review-fix entry must represent exactly one review issue id. The developer then runs `agent-up commits next` to stage each entry in isolation, reviews the diff in their editor, and commits manually.

Agent responsibility: manage queue entries only through structured MCP commit queue tools. Never `commits next`, never `git add`, never `git commit`, never `git stash`. After all enqueue or queue-editing calls, run `get_commits_status` so the developer can see the queue — then stop. The developer runs `commits next` themselves.

Before starting a new coding task, agents should run the structured MCP `guard_commits` tool for the current repository/worktree. If it fails, stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

Agents should use structured commit queue MCP tools for enqueue, queue inspection, metadata edits, file assignment, edit sessions, archive/restore, clear, and guard operations instead of shelling through commit CLI commands. `commits next` remains developer-only because it stages files and advances the review queue.

The MCP `enqueue_commit` tool intentionally restores tracked files to their pre-change state after saving the queued patch. Agents must treat that restoration as expected queue behavior and must not re-apply or modify those files after a successful enqueue; the queue owns them until the developer runs `agent-up commits next`.

MCP servers cannot register server-side post-job lifecycle hooks. Claude Code users can wire a client-side `Stop` hook to run `agent-up commits guard` and print a reminder when tracked files are dirty but not assigned to a queued entry:

```json
"hooks": {
  "Stop": [
    {
      "matcher": "",
      "hooks": [
        {
          "type": "command",
          "command": "agent-up commits guard 2>/dev/null | grep -q 'modified file(s) are not assigned' && echo '[agent-up] Unqueued changes detected - run: agent-up commits enqueue' || true"
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
  --files <file1> [file2 ...]
```

The CLI example above is developer-only. Agents use `enqueue_commit`.

One `enqueue` call per logical vertical slice. All files for a slice go in a single entry. Scope each conventional commit message to the queued slice, and follow any repository-specific `prompts.commitPolicy` guidance in `agent-up.json`. Cross-slice guidance or documentation updates must be queued in a separate guidance/docs entry instead of being bundled into an implementation slice. When feature-sliced paths under `Features/<Slice>/` are present, MCP enqueue tools reject cross-slice file groups and mismatched slice labels. Enqueue entries in the order they should be committed.

Mutating commit queue operations are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect in progress. Finish or abort that Git operation before changing or advancing the queue.

Use `agent-up commits changes` to inspect the working tree and queue assignment instead of composing raw `git ls-files`, `find`, `grep`, `tr`, or similar shell pipelines.

Queued entries must be manipulated through the commit queue commands:

```bash
agent-up commits inspect <entry>
agent-up commits message <entry> --message "<conventional commit message>"
agent-up commits files <entry> --add <file1> [file2 ...]
agent-up commits files <entry> --remove <file1> [file2 ...]
agent-up commits remove <entry>
agent-up commits restore <entry-id>
```

To change an existing queued patch, use an explicit edit session:

```bash
agent-up commits edit begin <entry>
# modify only files owned by that entry
agent-up commits edit save
```

The working tree must be clean before starting an edit session. `edit save` rejects cross-cutting changes outside the entry's file list; add same-slice files with `agent-up commits files <entry> --add ...` before saving. Use `agent-up commits edit abort` to discard the working-tree edit and keep the original queued patch.

Before any operation that would publish work outside the local workspace, run:

```bash
agent-up commits guard
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

The Agent-Up main release workflow publishes `@agent-up/audit` to npm with the planned release version when `NPM_TOKEN` is configured. The same release also publishes the Server container and `agent-up-helm` chart to Docker Hub (`themassiveone/agent-up-server` and `themassiveone/agent-up-helm`) when `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` are configured. Helm `capabilities` values list every first-party capability as an object with `disabled` and `versions`, plus optional `command`, `arguments`, and `versionArguments` for ACP adapters, then write enabled entries to Agent-Up capability inventory the same way NixOS `services.agent-up.capabilities` does. The Server image includes `git` and the Codex, Cursor, and Claude ACP CLIs from a repository-locked npm install and checksum-verified Node and Cursor archives, and links the nested `codex` CLI next to `codex-acp` for ChatGPT device-code subscription login; chart defaults enable those three ACP capabilities against `/opt/agent-up/bin`.

Installer and packaging behavior is testable product behavior. `agent-up verify run linux-smoke` publishes the Linux payloads and packaging/smoke tools, creates the Ubuntu package, and validates the packaged Server and CLI locally; because it performs full self-contained publishes it belongs to the `platform` tier rather than the fast tier. macOS and Windows smoke checks remain CI-only because they consume cross-published artifacts on their native runners. Shared installer planning, payload, adapter, progress, validation, per-component install/update/uninstall/repair, and platform install contracts belong in `LocalInstaller.Core`, with matching tests in `LocalInstaller.Core.Tests`. The shared InstallerApp UX belongs in `LocalInstaller.App`, with Avalonia headless tests in `LocalInstaller.App.Tests` and native-display Agent-Up flow tests in `AgentUp.Tests`; the dashboard includes an explicit refresh action that rechecks installed component and capability-module state for newly available versions. Product entrypoints use the LocalInstaller fluent API to register typed product and artifact manifests; each installable executable owns its artifact manifest, and `Program.cs` files should stay limited to product, installer option, and app startup configuration with no platform-specific installer plumbing. Multiple installer options may share a target category such as CLI or Server, but each option must have a unique artifact ID and payload directory. The installer app uses real platform adapters by default when `AGENTUP_INSTALLER_PAYLOAD_ROOT` points at a staged payload, supports noninteractive operation smoke through `AgentUp.InstallerApp --smoke-installer-operations --payload-root <payload-root>` that exercises individual component operations before bundled core install, treats Server as including tray payload and login autostart, and tests opt into fake adapters with `AGENTUP_INSTALLER_FAKE=1`. Native package formats should wrap or launch that dashboard rather than owning divergent install flows. Ubuntu package postinstall must install the dashboard launcher without auto-launching it; Ubuntu Desktop and InstallerApp launchers declare `StartupWMClass` for taskbar icon matching. Windows installer-owned tray autostart is machine-level so elevated install context does not register only the administrator user. Release artifact staging, package metadata generation, and native packaging tool orchestration belongs in `LocalInstaller.Packaging`, with matching tests in `LocalInstaller.Packaging.Tests`; thin `AgentUp.Packaging` only registers Agent-Up product metadata and delegates to LocalInstaller. CI packaging must use prebuilt InstallerApp, Desktop, Server, CLI, Tray, Packaging, PackageSmoke, and AgentUp.Tests artifacts from the Ubuntu .NET payload job so native release runners do not restore, build, or test product .NET projects. Native package jobs wait on the payload, test, GUI test, and coverage jobs plus version. CI builds `Plugins/Jetbrains` with the planned release version injected through Gradle and publishes `agent-up-jetbrains-plugin.zip` as a GitHub release asset. When `JETBRAINS_MARKETPLACE_TOKEN` is configured, CI also publishes the JetBrains plugin to Marketplace after the GitHub release succeeds. Shared package and installed-service smoke validation belongs in `LocalInstaller.Smoke`, with matching tests in `LocalInstaller.Smoke.Tests`; thin `AgentUp.PackageSmoke` only registers Agent-Up smoke product metadata and delegates to LocalInstaller. PackageSmoke accepts `--product-manifest <path>` so package, installed-service, and installer-flow smoke can run for a second product without recompilation. Installed-service smoke installs the native package, runs the installed InstallerApp with its installed payload root and `--install-core`, then delegates service, CLI, diagnostics, and uninstall checks to PackageSmoke. Native package assets stay under `packaging/` and should consume shared installer contracts rather than accumulating untested script-only behavior.

The standalone LocalInstaller repository owns the LocalInstaller release workflow. It plans versions with semantic-release using `localinstaller-v${version}` tags, builds/tests/packs `localinstaller.sln` with the planned `LocalInstallerVersion`, publishes self-contained `LocalInstaller.Sample.*` payloads from the Ubuntu build leg, packages those sample payloads on native Ubuntu, macOS, and Windows runners through `LocalInstaller.Sample.Packager`, smoke-validates them through `LocalInstaller.Sample.Smoke`, and creates a `main`-only GitHub release containing `LocalInstaller.*.nupkg` plus sample native installer assets. NuGet publishing is optional and must run only when `NUGET_API_KEY` is configured.

Windows package product identity must come from the product manifest: WiX product and bundle metadata, service name, safe CLI shim filename, registry keys, shortcuts, upgrade GUID, product-scoped component and bundle GUIDs, MSI sidecar name, and bootstrapper name are product-branded. The Agent-Up manifest must continue to produce the existing `agent-up-windows-<rid>` artifact names and WiX command shape.

Packaging request/product DTOs belong to `LocalInstaller.Packaging`; packaging code may map them to explicit platform installer contracts but must not depend on installer workflow product/session internals. Package request boundaries must validate the complete product manifest before artifact names, install paths, WiX identity, service names, shim filenames, server URLs, or command arguments are generated.

All `LocalInstaller.Packaging` filesystem access must pass through shared path validation in `Shared/Providers/PackagePathValidator` before reading, writing, copying, deleting, or creating directories. Package output directories are repository-relative and must remain under the repository root; prebuilt payload roots may be absolute CI-provided paths or repository-relative paths normalized under the repository root.

Product packaging wrappers must set `LOCALINSTALLER_REPOSITORY_ROOT` to the product repository root before invoking a published packaging entrypoint, because self-contained executables run from their extraction directory.

All `LocalInstaller.Smoke` process execution must pass through validated command providers. Smoke validation may execute native package managers, service tools, installed CLIs, Git, and capability-backed sample app lifecycle commands, but execution must choose from allowlisted command names before `ProcessStartInfo` is created. Artifact paths, installed executable paths, working directories, arguments, product metadata, and environment keys stay data and must be validated before use.

macOS `.pkg` artifacts install only `Agent-Up Installer.app`. The installer app owns the dashboard install and maintenance flow and contains a bundled offline payload with Desktop, Server, and CLI bits; it may also resolve an online latest payload when that capability is implemented. Desktop, Server, CLI, launchd registration, symlinks, validation, and uninstall behavior must stay in the InstallerApp/macOS adapter path, not in direct macOS package components. macOS installed-service smoke is skipped until InstallerApp-driven service installation is enabled in CI after package installation.

Packaging from NixOS or other non-native hosts should use the wrapper scripts in `scripts/package-*.sh`, which enter target-specific shells from `packaging/nix/` before delegating to the packaging entrypoint. NixOS installs Agent-Up declaratively through generated NixOS/Home Manager module options; `AgentUp.InstallerApp` is still shipped as a lookup-only dashboard through `agent-up-installer`, with install/update/uninstall actions disabled and capability versions read from Agent-Up capability inventory. Runtime capability lookup reads `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then `/etc/agent-up/capabilities.json`, `~/.config/agent-up/capabilities.local.json`, `~/.config/agent-up/capabilities.json`, and `.agent-up-dev/capabilities.json` walking up from the Server working directory, merging entries by capability id. Earlier files win for a field; later files fill unspecified fields, so the user overlay takes precedence over the user installment file. Server and Desktop installments share that inventory; entries may declare `command`, `arguments`, and `versionArguments` so Codex, Cursor, and Claude adapters launch a PATH name or a rooted version on disk instead of a hardcoded executable. First-party .NET and Docker discovery still probes common platform package-manager records. `nix-shell shell.nix` writes a local inventory under `.agent-up-dev` for workstation ACP testing. Installed-service smoke launches one .NET app and one Docker app through capability declarations and validates individual app stop/start plus workspace stop unless `AGENTUP_CAPABILITY_SMOKE_SKIP_REAL=1` is set for constrained runs; the generated .NET smoke app is restored and built before `agent-up start` so lifecycle validation does not depend on first-run SDK restore/build timing. The Docker sample uses `nginx:alpine` on Linux and macOS and a matching Windows IIS image on Windows runners, with `AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE` available for CI pre-pull/override. macOS packaging still requires Darwin because Apple package, signing, and notarization tools are not available on Linux.

Read: `docs/developer-guide/repo/packaging.md`.

## Design Principles

Agent-Up must remain framework agnostic, cross-platform, declarative, and zero-touch for application source code.

`AgentUp.DesignSystem/` is the single source of truth for all Agent-Up product UI,
documentation, screenshots, illustrations, and marketing presentation. Its
canonical sources are HTML/CSS plus the structured brand voice contract; its
generated React Native objects and Avalonia resources **and styles** are hard
dependencies of Mobile and Desktop. Mobile applies compiled component styles
(`auBox` / `auText`); Desktop applies generated Avalonia styles through catalog
classes. Never add a raw product color to Desktop, Mobile, docs, or marketing
when a semantic design-system role exists. Never restyle a catalog control from
tokens when a component style already exists. Never edit
generated files under `AgentUp.DesignSystem/dist/`; change the canonical CSS or
HTML catalog and regenerate them.

Desktop is the reference rendering. Use the near-black canvas, the raised surface ramp so cards and fields read as containers, alpha hairlines for structure, and off-white hierarchy. Interaction is neutral: hover and pressed use `state-hover` / `state-active`, never an accent fill. Green carries meaning only — primary action, selection, progress, healthy state — and selection is an accent tint plus a 2px accent rule, never a saturated fill. Emphasis follows the information hierarchy: the primary selection on a screen takes the accent, secondary selections stay neutral. Radius scales with the object (`lg` for panels, `xl` for panes and dialogs), and working regions are inset panes on the canvas rather than full-bleed panels butted at 1px lines. Product chrome uses the `ui` type tier and the `ui` weight roles; 700+ is for content and marketing, not 11-13px labels. Product screens use page-title and field-label, not marketing display type. Ambient neon glow, decorative green grids, green outlines around every
surface, and accent-tinted hover states are retired. Public claims must follow the naming, positioning, and
`Available`/`Preview`/`Experimental`/`Planned` lifecycle language in
`AgentUp.DesignSystem/brand/voice.json`. Real current product screenshots are
preferred over reconstructed interfaces; planned UI must be labeled visibly.

Read: `docs/developer-guide/repo/design-system.md`.

Read: `docs/developer-guide/repo/design-principles.md`.

## Roadmap

Agent-Up should evolve into the runtime operating system for AI-assisted development while Git manages source, Docker manages containers, and IDEs manage editing.

Read: `docs/user-docs/start/roadmap.md`.
