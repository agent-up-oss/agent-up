---
title: Desktop
---

# AgentUp.Desktop

`AgentUp.Desktop` is the human UI for Agent-Up.

Technology:

- .NET 10
- Avalonia 12 (Fluent dark theme)
- ReactiveUI (MVVM)

## Structure

The project follows capability-oriented slices:

```text
AgentUp.Desktop/
  Features/
    Workspaces/
      Http/         WorkspaceApiClient, WorkspaceDto
      ViewModels/   MainViewModel, WorkspaceItemViewModel
      Views/        MainWindow.axaml
```

## Responsibilities

The Desktop displays:

- Workspaces.
- Browser tabs.
- Logs.
- Diagnostics.
- Health.
- Running processes.

It connects to the Server and renders server-owned state.

## First-Run Tutorial

On first start, the Desktop shows a required setup tutorial over the normal application shell unless the user has already completed or skipped it. Tutorial progress is stored in the Desktop user settings file under the user's local application data directory.

The tutorial is a Desktop concern because it guides local workstation setup before the user can use Agent-Up successfully. It must not duplicate Server orchestration state. It may run local readiness checks that are prerequisites for using the Desktop, such as verifying that the Docker CLI is installed, that the Docker engine is responding, and that Agent-Up CLI can be invoked.

The first-run flow is:

1. Check Docker and Agent-Up CLI. Docker CLI availability is checked with `docker --version`; engine readiness is checked with `docker info`. Agent-Up CLI is checked with `agent-up --help`; if that is unavailable, Desktop may fall back to `dotnet run --project <repo>/AgentUp.CLI/AgentUp.CLI.csproj -- --help` when the CLI project can be inferred from the Desktop binary location. The user cannot continue until Docker, the Docker engine, and one Agent-Up CLI path work.
2. Choose a development environment from a visual grid. The JavaScript path is the active path and creates a React SPA dashboard, Express API, and Postgres sample.
3. Check Node.js by running `node --version` and `npm --version`. The JavaScript path requires Node.js 20 or newer. If Node is installed while Desktop is already running, the tutorial tells the user to restart Desktop so the process receives the updated `PATH`.
4. Create the sample project files. Desktop chooses a fresh path shaped like `/tmp/<random-guid>/agent-up-tutorial/example-agent1` and writes the React dashboard, Express product API, Postgres seed data, and `docker-compose.yaml`. The Express API serves a built-in OpenAPI explorer at `/` and the raw OpenAPI document at `/openapi.json`. If the step is repeated and the previous path already contains project files, Desktop chooses a new GUID root. The smoke check for this step is that the expected project files, including `docker-compose.yaml`, exist. Desktop also provides an Open File Explorer button for the generated folder.
5. Create `agent-up.json` at the project root. The tutorial shows the copyable file and also provides an auto-create button. The check for this step is that `agent-up.json` exists and is parseable with at least one application.
6. Run `agent-up start` in the sample project directory. The tutorial provides an automatic button that uses `agent-up start` or the local CLI project fallback. The check for this step is that the Server registered a workspace containing `React SPA`, `Express API`, and `Postgres`.
7. Duplicate the sample directory. Desktop copies `example-agent1` to `example-agent2` under the same `/tmp/<random-guid>/agent-up-tutorial/` root, swaps in different product seed data, and runs `agent-up start` in the duplicate. The tutorial checks that two matching workspaces exist and that their allocated ports do not collide.
8. Show a success page explaining that the user has just created two isolated workspaces from the same JavaScript project, that the second workspace intentionally shows different product data in the dashboard, and why Server-owned workspace identity, processes, Docker, browser profiles, diagnostics, and ports matter.

Completing the final step saves the tutorial as completed. The Skip button hides the entire tutorial and persists skipped state, so it does not reappear on the next launch.

Multi-action steps reveal one section at a time. Follow-up actions such as Open File Explorer, smoke checks, `agent-up.json` checks, workspace checks, and duplicate checks should only appear after the preceding action has been taken. The Continue button remains disabled until the step's final check succeeds and the result banner is visible. The project-file step renders a directory tree from .NET filesystem APIs instead of relying on a platform `tree` command. The `agent-up start` step shows command output before revealing the Server workspace check.

Users can navigate back to a previous step. Going back invalidates that step and all later step checks, so the user must complete them again.

The tutorial is rendered as an overlay above the full Desktop window. The underlying application remains visible behind a dimmed blurred layer, but the overlay consumes input while it is visible.

Console output should render as one wrapped, multiline-selectable text surface for copying diagnostics, but not editable.

Generated tutorial sample dependencies should be pinned to known-compatible versions instead of `latest`, so the first-run flow does not break because of upstream package engine changes. The generated `agent-up.json` commands clear stale `node_modules` and `package-lock.json` before installing, so rerunning the tutorial does not keep an incompatible Vite or native bundler package from an older sample. The generated Postgres command starts the Compose service and then streams `docker compose logs -f database`, so the Desktop console shows database readiness instead of only detached Compose status lines.

Native WebView surfaces must be hidden while the first-run tutorial is visible. Native browser widgets can render outside normal XAML z-order, so the Desktop explicitly hides active WebViews and browser error banners during onboarding and restores the active WebView after the tutorial closes.

While the tutorial overlay is visible, Desktop reloads the workspace list and asks the active browser view to reload after every step transition. This keeps the application state behind the overlay current without letting Desktop own workspace orchestration.

Native Desktop E2E tests set `AGENTUP_SKIP_FIRST_RUN_TUTORIAL=1` so onboarding does not cover the application surface under test.

## Browser Experience

The desktop should visually align with the interactive demo on the docs marketing page: compact dark chrome, green/teal active states, rounded workspace entries, and a browser-first runtime surface.

The app owns its window chrome. Do not rely on the host Xorg/desktop title bar for primary controls. Sidebar toggle, workspace reload, Server connection badge, title, and window controls are built into the top navigation area so screenshots and the real desktop app use the same frame. Window controls sit on the top right in Windows order: minimize, restore, close. The Server badge sits on the left after the sidebar/reload controls and is green when the Desktop can reach the Server and red when it cannot.

Desktop sets a runtime `WindowIcon` from `media/logo.png` so Linux/Xorg window switchers can display the app icon. The Desktop project must also declare `ApplicationIcon` pointing at `media/logo.ico`; Windows shell surfaces such as Alt+Tab use the executable icon resource rather than only Avalonia's runtime window icon.

```text
+---------------------------------------------------------------+
| ☰ ↺ SERVER ONLINE              Agent-Up               _  □  × |
+---------------------------------------------------------------+
| Agents | Frontend  Admin  Swagger  Logs                       |
| Agent1 |------------------------------------------------------|
| Agent2 |                                                      |
|        |        Active Browser Session                        |
|        |                                                      |
+---------------------------------------------------------------+
```

The left side shows workspace selection, health, branch, and running state. Sidebar collapse and reload are controlled from the title bar so the sidebar rail remains dedicated to workspace content. Expanded workspace rows fill the sidebar width, use the last segment of the repository path as the title, show the branch underneath, and expose the full repository path as the hover tooltip. The top area shows browser tabs, logs, and diagnostics. The center contains the embedded browser.

## Application Tabs

The first tab row lists the applications configured for the selected workspace. Selecting an application rebuilds the second tab row for that application.

For applications with configured ports, the second row starts with ports in `agent-up.json` order and automatically selects the first configured port. This makes the app's primary browser surface the default when switching between applications. Console remains available after the port tabs.

For applications without configured ports, Console is selected by default.

When the selected tab is an HTTP port, the Desktop shows a third row with back, forward, and reload controls followed by an editable browser address field. The field contains the full URL, such as `http://localhost:3000/`, for the selected port. Pressing Enter in the field navigates the workspace WebView to that URL, and successful HTTP/HTTPS WebView navigations update the field to the current page URL. Non-HTTP ports do not show the address row.

Because the native WebView does not always raise managed navigation updates for in-page link clicks, the Desktop also polls `window.location.href` for the active HTTP WebView and mirrors HTTP/HTTPS changes into the address field.

Reloading workspaces keeps the selected workspace by ID but rebinds it to the refreshed Server state, so the selected application's active HTTP port is navigated again after a sidebar reload.

## Browser Profile Isolation

Each workspace gets its own `NativeWebView` instance and platform-native profile storage. The `EnvironmentRequested` handler maps `BrowserUrlStore.ProfilePath(workspaceId)` to GTK/WPE data and cache directories on Linux, WebView2 user data folders on Windows, and WKWebView data store identifiers on macOS.

Cookies, local storage, cache, and navigation state must be shared by applications within the same workspace and isolated from every other workspace.

## Thin Client Rule

The Desktop does not own runtime state and should not duplicate orchestration rules from the Server.

## Installed Runtime

Installed Desktop artifacts are paired with a local `AgentUp.Server` service. The installer or package service assets are responsible for installing and starting `agent-up-server`; the Desktop still behaves as a client and connects to `http://localhost:5000` by default.

For development, `AGENTUP_SERVER_URL` can point Desktop at a manually started Server.
