# General

Agent-Up is a .NET solution for managing AI-assisted development workspaces.

It is not an application framework, deployment tool, IDE, or application orchestrator. Agent-Up owns the running development environment around applications: worktrees, processes, ports, Docker lifecycle, browser profiles, diagnostics, event history, and automation surfaces.

The authoritative product and implementation documentation lives in:

- User docs: `docs/user-docs/`
- Developer guide: `docs/developer-guide/`

# Definition Synchronization

`AGENTS.md` and the relevant docs pages are definition sources for the project.

Any change that alters behavior, architecture, ownership, project layout, workflows, configuration shape, runtime contracts, testing rules, or implementation guidance must update the matching definition source in the same change.

If code or docs are derived from the current state of `AGENTS.md`, update `AGENTS.md` when that definition changes. If `AGENTS.md` points to a docs page for the detailed definition, update that docs page when the source definition changes.

Do not leave implementation, `AGENTS.md`, and docs disagreeing. If the requested change intentionally supersedes existing guidance, update the guidance first or as part of the same commit.

# Architecture

Agent-Up is organized around one rule:

**AgentUp.Server is the single source of truth.**

Desktop, CLI, MCP clients, and future integrations are clients of the Server. They may display state and request actions, but they must not own runtime state or duplicate orchestration logic.

Expected solution shape:

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

Project directories live directly at the repository root and are included in the root solution. Do not introduce `src/` or `tests/` wrapper directories unless the repository is intentionally reorganized everywhere.

The exact project list may evolve, but ownership must not drift:

| Area | Owns |
|---|---|
| `AgentUp.Server` | Workspace registry, process lifecycle, ports, Docker, browser lifecycle, diagnostics, event recording, MCP, REST API |
| `AgentUp.Desktop` | Avalonia UI, workspace display, logs, diagnostics, embedded/shared browser views |
| `AgentUp.CLI` | Thin human-friendly command wrapper over Server capabilities |
| MCP clients | Automation interface; no local orchestration |
| `AgentUp.Shared` | Cross-boundary contracts only when genuinely shared |

Read the full architecture guide before making structural changes: `docs/developer-guide/architecture.md`.

# New Architecture

The project should be implemented as capability-oriented slices inside each owning project rather than broad technical buckets.

Prefer this:

```text
AgentUp.Server/
  Features/
    Workspaces/
      Controllers/
      Services/
      Repositories/
      DTOs/
    Processes/
      Services/
      Diagnostics/
    Applications/
      Controllers/
      DTOs/
    Browser/
      Services/
      Profiles/
      Automation/
    Ports/
      Services/
      Models/
    Mcp/
      Tools/
      Resources/

AgentUp.Desktop/
  Features/
    Workspaces/       (sidebar navigation: workspace list, loading, error, collapse)
      Http/
      ViewModels/
      Views/
    Applications/     (application tab bar: list and selection per workspace)
      Http/
      ViewModels/
    Console/          (console output/logs for the selected application)
      Http/
      ViewModels/
    Ports/            (port sub-tabs: HTTP browser view, TCP info, probe status)
      ViewModels/
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

## Migrations And Persistence

If persistent storage is introduced, migrations stay together in the owning infrastructure/migration location for the project.

Feature separation happens at repository/service boundaries. Do not scatter migration files by feature unless the project explicitly adopts that convention later.

## Inter-Slice Communication

A slice owns its writes.

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

## CLI

The CLI is a thin developer convenience wrapper over Server capabilities.

It should forward commands such as restart, stop, status, and logs to the Server. User guide: `docs/user-docs/cli.md`.

## MCP

MCP is the primary automation interface for AI agents.

Agents should use MCP directly instead of shelling through the CLI when browser inspection, interaction, diagnostics, logs, screenshots, or Playwright generation are needed. Full guide: `docs/developer-guide/mcp.md`.

# Configuration Rules

Every managed repository is described declaratively with `agent-up.json`.

Applications must not reference Agent-Up packages, SDKs, or APIs. Agent-Up injects runtime values through environment variables and process launch configuration.

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
| `AgentUp.Desktop` | `AgentUp.Desktop.Tests` |
| `AgentUp.CLI` | `AgentUp.CLI.Tests` |

`AgentUp.Tests` is a separate cross-product E2E project that exercises the full Desktop application against a real display (Xvfb) and WebKitGTK. These tests are part of the normal test run; Xvfb is managed by the test infrastructure so they work in CI without a physical display.

Forbidden:

- Changing production behavior without updating or adding tests for that behavior.
- Adding REST endpoints or MCP tools without tests for the new contract.
- Changing request/response/resource shapes without updating tests.
- Removing behavior without removing or updating tests that covered it.
- Claiming completion while relevant tests are missing, skipped, or known broken.

## Test Structure

Tests should follow the same feature/slice layout as production code.

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
- Repository/infrastructure tests verify persistence, filesystem, process, Docker, or browser integration behavior with realistic dependencies when practical.
- End-to-end workspace lifecycle tests should be few and prove full integration across Server, process management, ports, diagnostics, and browser state.

Avoid duplicate tests that assert the same rule through multiple layers.

# Content Sections

The sections below intentionally introduce each concept briefly and point to the canonical docs page. Keep AGENTS.md concise; detailed specifications belong in `docs/`.

## Workspace

A workspace is the unit of isolation for an agent or developer session. It is identified by project path and may include repository/worktree metadata, branch, commit, browser profile, Docker infrastructure, running processes, allocated ports, diagnostics, and event history. Non-Git project paths are valid and should display as `not on a git branch`.

Read: `docs/user-docs/workspace.md`.

## Configuration

Agent-Up uses declarative repository configuration through `agent-up.json`. Applications declare launch commands, port environment variables, browser paths, and Docker setup without source-code integration.

Read: `docs/user-docs/configuration.md` and `docs/user-docs/agent-up-json.md`.

## Browser

Agent-Up keeps browser sessions tied to workspaces so developers and agents share authentication and navigation state. Restarting applications should reload the existing workspace browser session rather than create more tabs.

Read: `docs/user-docs/browser.md` and `docs/user-docs/browser-profiles.md`.

## Server

The Server is the runtime authority for Agent-Up. It owns orchestration, state, lifecycle, diagnostics, MCP, and REST APIs.

Read: `docs/developer-guide/server.md`.

## Desktop

The Desktop is the Avalonia UI for humans. It presents Server-owned state and shared browser sessions.

**Per-workspace browser isolation:** Each workspace gets its own `NativeWebView` instance. Isolation is achieved by handling the `EnvironmentRequested` event to point GTK data and cache directories at a workspace-scoped path (managed by `BrowserUrlStore.ProfilePath`). The last-visited URL per workspace is persisted by `BrowserUrlStore` and restored when the workspace is reopened.

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

## Design Principles

Agent-Up must remain framework agnostic, cross-platform, declarative, and zero-touch for application source code.

Read: `docs/developer-guide/design-principles.md`.

## Roadmap

Agent-Up should evolve into the runtime operating system for AI-assisted development while Git manages source, Docker manages containers, and IDEs manage editing.

Read: `docs/user-docs/roadmap.md`.
