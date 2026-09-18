---
title: Diagnostics
---

<DocEyebrow slice="Diagnostics" status="preview" />

# Diagnostics

<DocFocus>
If browser navigation or a start fails, read the workspace console before retrying the browser.
</DocFocus>

**Owner:** `AgentUp.Server` diagnostics and audit. Tests live in Server diagnostics/audit suites. MCP: `/mcp/audit` plus Orchestration `get_workspace_diagnostics` and `get_workspace_console`. REST: `/api/diagnostics/workspaces/{workspaceId}` and `/api/audit/...`.

## What it is

The Server continuously collects console output, JavaScript exceptions, failed network requests, performance timings, health information, and process status. Product crash reporting for Agent-Up itself stays in [telemetry](/developer-guide/repo/telemetry).

<DocSpine>
<DocBeat selected>Inspect `get_workspace_console`</DocBeat>
<DocBeat>Read health and process state</DocBeat>
<DocBeat>Query durable audit history when you need the record</DocBeat>
</DocSpine>

<DocContract>get_workspace_console</DocContract>

Next in this slice: [Event recording](/developer-guide/diagnostics/events).

## Exposure

The canonical workspace snapshot is `GET /api/diagnostics/workspaces/{workspaceId}`. It combines current workspace/application process state, configured health results, bounded recent logs, and relevant durable audit events. An optional `application` query parameter narrows both application snapshots and event entries. `logLimit` is capped at 1,000 lines per application and `entryLimit` at 500 events.

Each event entry has a normalized category, severity, and `active` or `resolved` state. It also carries the affected application and browser-session identifier when the source supplied that context. Application context is accepted from the established `application`, `applicationName`, and `appName` audit detail keys.

Diagnostics are exposed through Desktop, CLI `diagnostics`, and Orchestration MCP `get_workspace_diagnostics`. The Desktop Diagnostics tab keeps a bounded in-memory window of application audit events centered on the current page (±5 pages). Refresh and pagination fetch only that window from the Server on demand; live Server-Sent Events prepend matching events while you stay on page 1. Category toggles, search, and pagination run against the local window without loading the full audit history. Search filters as you type across action, kind, outcome, and detail text. Refresh reloads the current window from the Server even while streaming is enabled. Changing filters, search, toggling streaming, or refreshing returns to page 1 unless refresh explicitly reloads the current page. Pagination uses numbered page buttons ±3 around the current page plus first/last page jumps on the filtered local result set.

Orchestration MCP exposes `get_workspace_console` for a bounded live snapshot of application console output and the recent durable console audit trail for a workspace.

Per-application audit pages use a composite timestamp and event-ID cursor so events sharing a timestamp are neither skipped nor repeated. Application-scoped queries read compact per-application index files that store byte offsets into the canonical daily JSONL log, seek-read only matching rows, and lazily backfill an index the first time an application/day pair is queried. Workspace-wide queries without an application filter still use reverse JSONL scanning with a lightweight line prefilter. Initial Desktop load requests only the first filtered page from the Server and reuses the in-memory cache when you leave and return to the Diagnostics tab.

The REST endpoint is `GET /api/audit/workspaces/{workspaceId}/applications/{application}`; live updates use `GET /api/audit/workspaces/{workspaceId}/applications/{application}/stream`.

## Audit MCP

`/mcp/audit` exposes Streamable HTTP and legacy SSE at `/mcp/audit/sse` plus `/mcp/audit/message`. It owns durable audit history queries and Server-managed artifact loading.

- `audit_query`: filters durable audit events by workspace, working-directory id, repository path, branch, commit, event kind, source, outcome, and time range.
- `audit_timeline`: returns compact recent history for agent context.
- `audit_get_event`: returns full details for one audit event.
- `audit_load_artifact`: loads a Server-managed artifact by opaque artifact id and can return inline image data when requested.

## Metrics and audit scopes

Metrics are recorded in the durable audit trail rather than a separate telemetry store.

| Scope | Source | What is recorded |
|---|---|---|
| `workspace` | default | Workspace/browser/process events (existing audit behavior) |
| `application` | Server pull | JSON metrics from `ports[].metrics` endpoints every 30 seconds |
| `host-server` | Server | Server process CPU, memory, thread, and GC heap samples every 30 seconds |
| `host-desktop` | Desktop | Desktop process samples posted to `POST /api/audit/record` every 30 seconds |

Query metrics with Audit MCP `audit_query` using `kind="metrics"` and `scope="host-server"`, `scope="host-desktop"`, or `scope="application"` plus an optional workspace id.

Application metrics endpoints are pull-based: declare `metrics` on a port in `agent-up.json` (same shape as `healthCheck`). The app returns JSON metrics without referencing Agent-Up packages.

## Purpose

Diagnostics make AI validation practical. An agent should be able to modify code, restart the workspace, inspect health, interact with the application, and retrieve evidence when something fails.
