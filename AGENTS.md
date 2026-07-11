# Agent-Up Design Specification

## Vision

Agent-Up is a cross-platform development workspace manager designed specifically for AI-assisted software development.

It is **not** an application framework, deployment tool, IDE or application orchestrator.

Instead, Agent-Up manages **development workspaces**.

Every AI agent operates inside its own Git worktree. Each workspace has its own runtime environment, browser session, infrastructure and application state.

Agent-Up exists to make switching between these workspaces effortless while allowing both developers and AI agents to interact with the exact same running applications.

---

# Problem Statement

Modern AI-assisted development introduces several challenges.

Every AI agent typically owns

* its own Git worktree
* its own branch
* its own runtime
* its own application state

However existing tooling assumes a single developer running a single application instance.

Typical problems include

* constantly starting multiple services
* Docker infrastructure collisions
* browser tab explosion
* duplicated authentication
* inconsistent runtime state
* manual process management
* difficult validation of AI-generated implementations

Agent-Up solves these problems without requiring changes to application source code.

---

# Core Principles

## Framework Agnostic

Agent-Up must support arbitrary web applications.

It must not contain knowledge about

* ASP.NET
* Spring
* React
* Next.js
* Angular
* Vue
* Express
* or any other framework.

Applications are described declaratively.

---

## Cross Platform

Supported platforms

* Windows
* Linux
* macOS
* NixOS

The desktop application should therefore be implemented using Avalonia.

---

## Zero Application Changes

Applications should never reference Agent-Up.

No SDK.

No package.

No runtime dependency.

Only environment variables supplied during launch.

---

# High Level Architecture

The system consists of four major components.

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
        |                  |                   |
+---------------+   +---------------+   +------------------+
| Avalonia UI   |   | AgentUp CLI   |   | MCP Clients      |
|               |   |               |   | ChatGPT          |
| Human UI      |   | Thin Wrapper  |   | Claude           |
|               |   |               |   | Codex            |
+---------------+   +---------------+   +------------------+
```

The **Server is the single source of truth**.

Every other component is a client.

---

# AgentUp.Server

The Server owns all runtime state.

Responsibilities include

* Workspace registry
* Process lifecycle
* Port allocation
* Docker lifecycle
* Browser lifecycle
* Browser profiles
* Browser session persistence
* Event recording
* Diagnostics
* Health monitoring
* Playwright generation
* MCP server
* REST API

The Server performs all orchestration.

No orchestration logic belongs anywhere else.

---

# AgentUp.Desktop

Technology

* .NET
* Avalonia

Responsibilities

* Display workspaces
* Display browser tabs
* Display logs
* Display diagnostics
* Display health
* Display running processes

The Desktop does **not** own runtime state.

It connects to the Server.

---

# AgentUp.CLI

Technology

* .NET Console

Purpose

Developer convenience.

The CLI simply forwards commands to the Server.

Example

```bash
agent-up restart

agent-up stop

agent-up status

agent-up logs
```

The CLI owns no state.

---

# MCP

The Server exposes an MCP server.

This is the primary automation interface.

The CLI becomes a human-friendly wrapper around MCP capabilities.

AI agents should use MCP directly.

---

# Configuration

Every repository root contains

```text
agent-up.json
```

Example

```json
{
    "name": "Inventory",

    "applications": [

        {
            "name": "Frontend",
            "command": "dotnet run --project src/Web",
            "portVariable": "WEB_PORT",
            "path": "/"
        },

        {
            "name": "API",
            "command": "dotnet run --project src/Api",
            "portVariable": "API_PORT",
            "path": "/swagger"
        }

    ],

    "docker": [

        "docker compose up -d"

    ]
}
```

No hardcoded ports are allowed.

---

# Workspace

A workspace consists of

* Repository
* Git worktree
* Branch
* Commit
* Browser profile
* Docker infrastructure
* Running processes
* Allocated port range
* Runtime diagnostics
* Event history

---

# Port Allocation

The Server owns all ports.

Every workspace receives a dedicated contiguous port range.

Example

```text
Workspace A

5000-5099

Workspace B

5100-5199

Workspace C

5200-5299
```

Applications reference only environment variables.

Example

```text
WEB_PORT=5100

API_PORT=5101

AUTH_PORT=5102
```

Applications must never assume localhost ports.

---

# Browser Model

Every workspace owns an isolated browser profile.

The Server manages browser instances.

The Desktop displays them.

Browser state includes

* Cookies
* Local Storage
* Session Storage
* IndexedDB
* Cache

Changing workspaces restores browser state instantly.

Restarting applications reloads the browser instead of creating new browser tabs.

---

# Browser Experience

The desktop resembles an IDE.

```text
+---------------------------------------------------------------+
| Agent-Up                                                      |
+---------------------------------------------------------------+
| Agents                    Frontend  Admin  Swagger  Logs      |
|---------------------------------------------------------------|
|                                                               |
|                 Active Browser Session                        |
|                                                               |
+---------------------------------------------------------------+
```

Left

* Workspace selector
* Health
* Branch
* Running state

Top

* Browser tabs
* Logs
* Diagnostics

Center

* Embedded browser

---

# Browser Abstraction

The browser implementation must be abstracted.

```csharp
public interface IBrowserHost
{
    Task NavigateAsync(Uri uri);

    Task ReloadAsync();

    Task<string> GetHtmlAsync();

    Task ClickAsync(string selector);

    Task FillAsync(string selector, string value);

    Task<byte[]> ScreenshotAsync();
}
```

Platform implementations can vary.

---

# MCP Resources

Example

```text
workspace://current

workspace://agent-1

workspace://agent-1/browser

workspace://agent-1/logs

workspace://agent-1/events

workspace://agent-1/frontend
```

Resources expose current workspace state.

---

# MCP Tools

Examples

Restart Workspace

Stop Workspace

List Workspaces

List Applications

Navigate Browser

Click Element

Fill Input

Press Keys

Take Screenshot

Export Playwright

Wait For Selector

Wait For Text

Wait For Navigation

Retrieve Logs

Retrieve Diagnostics

Retrieve Event Stream

---

# Browser Automation

AI agents interact with applications through MCP.

Example

```
inspect_page

↓

click

↓

fill

↓

press

↓

wait

↓

screenshot
```

The Server executes browser operations.

---

# Structured Inspection

The browser should expose

* Accessibility tree
* Interactive elements
* Page metadata
* DOM snapshot
* HTML
* Browser history
* Screenshot

Accessibility data should be preferred over raw HTML.

---

# Browser Session Sharing

One of the core ideas behind Agent-Up is that developers and AI agents share the same browser session.

Benefits

* Shared authentication
* Shared cookies
* Shared navigation
* Shared application state
* No duplicate browser windows
* No Playwright browser startup

---

# Event Recording

Every browser interaction becomes an event.

Examples

* Navigation
* Click
* Keyboard
* Text Entry
* DOM Mutation
* Console Message
* Network Request
* Screenshot
* Dialog
* Notification

This event stream becomes the canonical interaction history.

---

# Playwright Generation

Agent-Up can generate Playwright tests from recorded interaction history.

Generated tests should

* Prefer semantic locators
* Avoid brittle selectors
* Generate assertions
* Produce readable code
* Follow Playwright best practices

---

# Workflow Inference

Instead of exporting raw interaction history, Agent-Up should infer user intent.

Example interaction

* Open Orders
* Create Customer
* Add Products
* Submit Order
* Verify Success

Generated test

```
Creating an order succeeds
```

instead of

```
Click Button 17
```

Business workflows should be inferred automatically.

---

# Automatic Assertions

Agent-Up should infer assertions such as

* Success notification visible
* Navigation completed
* Validation error visible
* Button disabled
* URL changed
* Network request completed

The generated Playwright test should validate outcomes rather than replay interactions.

---

# Diagnostics

The Server continuously collects

* Console output
* JavaScript exceptions
* Failed network requests
* Performance timings
* Health information
* Process status

Diagnostics are exposed through MCP.

---

# AI Validation Workflow

A complete AI workflow

```
Modify Code

↓

Restart Workspace

↓

Wait Until Healthy

↓

Inspect Page

↓

Navigate

↓

Interact

↓

Validate

↓

Take Screenshot

↓

Generate Playwright Test

↓

Commit
```

No browser automation framework should be required outside Agent-Up.

---

# Long-Term Vision

Agent-Up evolves beyond being a process launcher.

It becomes the runtime operating system for AI-assisted development.

Git manages source code.

Docker manages containers.

The IDE manages editing.

Agent-Up manages the running development environment.

Developers and AI agents collaborate inside the same live workspace.

---

# Documentation Layout

The final repository documentation should be organized as

```text
AGENTS.md

docs/

    architecture.md

    workspace.md

    browser.md

    server.md

    desktop.md

    cli.md

    mcp.md

    configuration.md

    agent-up-json.md

    browser-profiles.md

    event-recording.md

    playwright.md

    diagnostics.md

    workflows.md

    design-principles.md

    roadmap.md
```

`AGENTS.md` should act only as an index and high-level overview.

Detailed specifications belong in the `docs/` directory.

---

# AI Agent Guidance

When implementing Agent-Up

* Keep the Server as the single source of truth.
* Keep Desktop, CLI and MCP clients thin.
* Preserve framework agnosticism.
* Never require application source changes.
* Keep configuration declarative.
* Prefer MCP over CLI for automation.
* Design every feature to support multiple concurrent workspaces.
* Preserve browser session isolation.
* Favor interfaces over platform-specific implementations.
* Record interactions as events rather than imperative commands.
* Treat the event stream as the canonical representation from which Playwright tests, diagnostics and future automation features can be derived.
