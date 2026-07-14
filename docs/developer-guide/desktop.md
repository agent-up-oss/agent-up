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

## Browser Experience

The desktop should visually align with the interactive demo on the docs marketing page: compact dark chrome, green/teal active states, rounded workspace entries, and a browser-first runtime surface.

The app owns its window chrome. Do not rely on the host Xorg/desktop title bar for primary controls. Sidebar toggle, workspace reload, Server connection badge, title, and window controls are built into the top navigation area so screenshots and the real desktop app use the same frame. Window controls sit on the top right in Windows order: minimize, restore, close. The Server badge sits on the left after the sidebar/reload controls and is green when the Desktop can reach the Server and red when it cannot.

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
