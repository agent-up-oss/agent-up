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

## Thin Client Rule

The Desktop does not own runtime state and should not duplicate orchestration rules from the Server.
