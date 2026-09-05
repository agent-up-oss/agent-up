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
Per-application audit pages use a composite timestamp and event-ID cursor so
events sharing a timestamp are neither skipped nor repeated. Repository queries
use the cursor timestamp to avoid loading newer daily audit files on later pages.

## Metrics And Audit Scopes

Metrics are recorded in the durable audit trail rather than a separate telemetry store.

| Scope | Source | What is recorded |
|---|---|---|
| `workspace` | default | Workspace/browser/process events (existing audit behavior) |
| `application` | Server pull | JSON metrics from `ports[].metrics` endpoints every 30 seconds |
| `host-server` | Server | Server process CPU, memory, thread, and GC heap samples every 30 seconds |
| `host-desktop` | Desktop | Desktop process samples posted to `POST /api/audit/record` every 30 seconds |

Query metrics with Audit MCP `audit_query(kind="metrics", scope="host-server")`, `scope="host-desktop"`, or `scope="application"` plus an optional `workspaceId`.

Application metrics endpoints are pull-based: declare `metrics` on a port in `agent-up.json` (same shape as `healthCheck`). The app returns JSON such as `{ "metrics": { "requests_total": 42 } }` without referencing Agent-Up packages.

## Purpose

Diagnostics make AI validation practical. An agent should be able to modify code, restart the workspace, inspect health, interact with the application, and retrieve evidence when something fails.
