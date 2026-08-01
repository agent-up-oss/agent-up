---
title: Diagnostics
---

# Diagnostics

The Server continuously collects runtime diagnostics for each workspace.

Diagnostics include:

- Console output.
- JavaScript exceptions.
- Failed network requests.
- Performance timings.
- Health information.
- Process status.

## Exposure

Diagnostics are exposed through MCP and displayed by the Desktop.
Orchestration MCP exposes `get_workspace_console` for a bounded live snapshot of application console output and the recent durable console audit trail for a workspace.

## Purpose

Diagnostics make AI validation practical. An agent should be able to modify code, restart the workspace, inspect health, interact with the application, and retrieve evidence when something fails.
