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

The canonical workspace snapshot is `GET /api/diagnostics/workspaces/{workspaceId}`. It combines current workspace/application process state, configured health results, bounded recent logs, and relevant durable audit events. An optional `application` query parameter narrows both application snapshots and event entries. `logLimit` is capped at 1,000 lines per application and `entryLimit` at 500 events.

Each event entry has a normalized category, severity, and `active` or `resolved` state. It also carries the affected application and browser-session identifier when the source supplied that context. Application context is accepted from the established `application`, `applicationName`, and `appName` audit detail keys.

Diagnostics are exposed through Desktop, CLI `diagnostics`, and Orchestration MCP `get_workspace_diagnostics`. The Desktop Diagnostics tab keeps a bounded in-memory window of application audit events centered on the current page (±5 pages). Refresh and pagination fetch only that window from the Server on demand; live Server-Sent Events prepend matching events while you stay on page 1. Category toggles, search, and pagination run against the local window without loading the full audit history. Search filters as you type across action, kind, outcome, and detail text. Refresh reloads the current window from the Server even while streaming is enabled. Changing filters, search, toggling streaming, or refreshing returns to page 1 unless refresh explicitly reloads the current page. Pagination uses « ‹ numbered page buttons ±3 around the current page › » plus first/last page jumps on the filtered local result set.
Orchestration MCP exposes `get_workspace_console` for a bounded live snapshot of application console output and the recent durable console audit trail for a workspace.
Per-application audit pages use a composite timestamp and event-ID cursor so
events sharing a timestamp are neither skipped nor repeated. Application-scoped
queries read compact per-application index files that store byte offsets into the
canonical daily JSONL log, seek-read only matching rows, and lazily backfill an
index the first time an application/day pair is queried. Workspace-wide queries
without an application filter still use reverse JSONL scanning with a lightweight
line prefilter. Initial Desktop load requests only the first filtered page from
the Server and reuses the in-memory cache when you leave and return to the Diagnostics tab.
The REST endpoint is `GET /api/audit/workspaces/{workspaceId}/applications/{application}`; live updates use `GET /api/audit/workspaces/{workspaceId}/applications/{application}/stream`.

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
