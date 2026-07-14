---
title: Desktop
---

# AgentUp.Desktop

`AgentUp.Desktop` is the human UI for Agent-Up.

Technology:

- .NET 9
- Avalonia 11 (Fluent dark theme)
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

The desktop should resemble an IDE:

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

The left side shows workspace selection, health, branch, and running state. The top area shows browser tabs, logs, and diagnostics. The center contains the embedded browser.

## Application Tabs

The first tab row lists the applications configured for the selected workspace. Selecting an application rebuilds the second tab row for that application.

For applications with configured ports, the second row starts with ports in `agent-up.json` order and automatically selects the first configured port. This makes the app's primary browser surface the default when switching between applications. Console remains available after the port tabs.

For applications without configured ports, Console is selected by default.

When the selected tab is an HTTP port, the Desktop shows a third row with back, forward, and reload controls followed by an editable browser address field. The field contains the full URL, such as `http://localhost:3000/`, for the selected port. Pressing Enter in the field navigates the workspace WebView to that URL, and successful HTTP/HTTPS WebView navigations update the field to the current page URL. Non-HTTP ports do not show the address row.

Because the native WebView does not always raise managed navigation updates for in-page link clicks, the Desktop also polls `window.location.href` for the active HTTP WebView and mirrors HTTP/HTTPS changes into the address field.

Reloading workspaces keeps the selected workspace by ID but rebinds it to the refreshed Server state, so the selected application's active HTTP port is navigated again after a sidebar reload.

## Thin Client Rule

The Desktop does not own runtime state and should not duplicate orchestration rules from the Server.
