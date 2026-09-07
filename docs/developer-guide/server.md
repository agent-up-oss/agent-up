---
title: Server
---

# AgentUp.Server

`AgentUp.Server` owns all runtime state and performs all orchestration.

## Responsibilities

The Server manages:

- Workspace registry.
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

## Single Source of Truth

Desktop, CLI, and MCP clients connect to the Server. They do not keep authoritative copies of workspace state.

## Service Hosting

Packaged installations run `AgentUp.Server` as the local `agent-up-server` service:

- macOS uses launchd.
- Windows uses Windows Service hosting.
- Ubuntu uses systemd.
- NixOS uses a systemd service definition through a Nix module.

Packaged services bind to `http://127.0.0.1:5000` by default. Service definitions that automatically restart the Server must throttle restart attempts to at least 5 seconds so a bind failure, such as another process already using port 5000, cannot create a tight restart loop.

## Authentication and network boundaries

The REST API uses a single administrator account. Set `AGENTUP_ADMIN_PASSWORD`
to enable authentication; when it is unset, REST routes remain open and clients
skip their login UI. A successful `POST /api/auth/login` returns an in-memory
bearer token. The fallback authorization policy requires a valid token for every
REST endpoint unless the endpoint explicitly opts out; this makes newly added
routes protected by default when authentication is enabled. `GET /api/auth/status`
and the login endpoint are anonymous so clients can decide whether to display
login.

Set `AGENTUP_AUTH_DISABLED=true` to force unauthenticated mode even when
`AGENTUP_ADMIN_PASSWORD` is configured.

MCP remains unauthenticated for local automation, but `/mcp` requests from a
non-loopback remote address are hidden with a 404 response. The HTTP listener
can therefore use an address such as `ASPNETCORE_URLS=http://0.0.0.0:5000` for
LAN REST access while MCP remains localhost-only at the request boundary.

The REST API permits cross-origin browser requests from any HTTP or HTTPS
origin, so the Mobile web/PWA client can reach a Server the user points it at
regardless of where that client is hosted (a local dev port, an installed
PWA, or a deployed preview build). The browser's own mixed-content policy
still applies: a client served over HTTPS cannot fetch a plain-HTTP Server
unless that Server is loopback-hosted, so a remote Server should be reachable
over HTTPS.

This service shape is packaging and lifecycle behavior only. Runtime ownership remains unchanged: all orchestration stays in `AgentUp.Server`, and Desktop stays a client.

This rule keeps concurrent agents, human developers, and automation clients aligned around the same running environment.

## Orchestration Rule

If a feature starts, stops, restarts, navigates, records, allocates, diagnoses, or exports workspace behavior, that logic belongs in the Server.

## Process Environment

When the Server launches a local application process, it injects the workspace's full allocated port map into the process environment. This lets sibling applications discover each other through declared variables such as `WEB_PORT`, `API_PORT`, and `POSTGRES_PORT` without coupling application source code to Agent-Up APIs.

Local application commands are parsed as an executable plus arguments and launched with `ProcessStartInfo.ArgumentList`. The Server rejects shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells before process start.

Managed local application processes also receive `AGENT_UP_AUDIT_ENDPOINT`,
`AGENT_UP_WORKSPACE_ID`, and `AGENT_UP_APPLICATION`. Browser builds can expose
these values to `@agent-up/audit`. The audit endpoint is derived from the
orchestrating Server's configured public/listen URL, including the development
port, rather than assuming the packaged port. The Server accepts frontend events into its
existing audit store and provides bounded, cursor-paginated queries scoped to a
workspace and application.

Process output storage must not use workspace IDs or application names as raw path segments. Repositories that persist process logs must encode or canonicalize those identifiers and verify the resolved path stays under the Server-owned output root before reading, writing, or deleting files.

## Tutorial Cleanup

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts, so stale workspace state cannot render behind onboarding or affect the guided sample setup.
