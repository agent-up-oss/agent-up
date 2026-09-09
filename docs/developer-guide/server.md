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

Packaged installs currently declare `AGENTUP_AUTH_DISABLED=true` on the native `agent-up-server` service so installed-service smoke and first-run Desktop/CLI use work without a preconfigured administrator password. To require REST authentication on a packaged install, remove or override that variable in the platform service environment, set `AGENTUP_ADMIN_PASSWORD`, and restart `agent-up-server`.

## Authentication and network boundaries

The REST API uses a single administrator account. Authentication is required
by default for every REST endpoint unless the endpoint explicitly opts out; this
makes newly added routes protected by default. `AGENTUP_ADMIN_PASSWORD` supplies
the administrator password for `POST /api/auth/login`, which returns an in-memory
bearer token. `GET /api/auth/status` and the login endpoint are anonymous so
Desktop, Mobile, and other clients can decide whether to display login. Bearer
sessions expire after 24 hours by default; override with
`AGENTUP_SESSION_LIFETIME_SECONDS`.

The Server starts even when `AGENTUP_ADMIN_PASSWORD` is unset. REST routes remain
protected in that mode, but login cannot succeed until the password is configured.

For local development, copy `.env.example` to `.env` in the repository root.
Server, Desktop, and CLI load that file on startup when present. Existing shell
environment variables are not overridden.

Set `AGENTUP_AUTH_DISABLED=true` to run without REST authentication. Desktop and
Mobile query authentication status and skip their login UI in that mode.

MCP remains unauthenticated for local automation, but `/mcp` requests from a
non-loopback remote address are hidden with a 404 response. The HTTP listener
can therefore use an address such as `ASPNETCORE_URLS=http://0.0.0.0:5000` for
LAN REST access while MCP remains localhost-only at the request boundary.

Desktop and Mobile reject remote `http://` Server URLs for administrator login
and require HTTPS outside loopback hosts.

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

When the Server launches a local application process, it injects the workspace's full allocated port map into the process environment. This lets sibling applications discover each other through declared variables such as `WEB_PORT`, `API_PORT`, and `POSTGRES_PORT` without coupling application source code to Agent-Up APIs. When multiple applications declare the same port variable name, each process still receives its own allocated port for that variable.

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
