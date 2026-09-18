---
title: Workspaces
---

<DocEyebrow slice="Workspaces" status="available" />

# Workspaces

<DocFocus>
`AgentUp.Server` owns workspace identity, start/stop, clones, and login. Clients display that state and request actions.
</DocFocus>

**Owner:** `AgentUp.Server` writes. Tests live in `AgentUp.Server.Tests/Features/Workspaces/` and `SourceClones/`. MCP: `/mcp/orchestration`. REST: `/api/workspaces`, `/api/source-clones`, `/api/auth`.

## What it is

A workspace is the unit of isolation. Desktop, Mobile, CLI, and MCP clients connect to the Server. They do not keep authoritative copies of workspace state. Packaged installations run `agent-up-server` on `http://localhost:5000`; the repository launch profile uses `http://localhost:5001`.

<DocSpine>
<DocBeat selected>Register or start from `agent-up.json`</DocBeat>
<DocBeat>Use the returned workspace id</DocBeat>
<DocBeat>Inspect status only when the user asked</DocBeat>
</DocSpine>

<DocContract>start_workspace</DocContract>

The Server sends MCP initialization instructions that tell clients to use `start_workspace` immediately when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up. Agents should not call `list_workspaces` or `get_workspace_status` first when the current repository/worktree is known. Agent-Up starts local development environments; it does not deploy to cloud infrastructure.

If `start_workspace` cannot find `agent-up.json`, it instructs the agent to read `docs/user-docs/configuration/index.md`, search for an existing `agent-up.json`, or ask the user before creating one.

Next in this slice: [Workflows](/developer-guide/workspaces/workflows).

## Orchestration MCP

`/mcp/orchestration` exposes Streamable HTTP and legacy SSE at `/mcp/orchestration/sse` plus `/mcp/orchestration/message`. It owns workspace tools, workspace resources, and Agent-Up context resources.

Resources:

```text
agent-up://context
agent-up://agent-up-json
agent-up://workspaces
agent-up://workspaces/{id}
```

Tools:

- `start_workspace`: registers or updates a workspace from its `agent-up.json`, then starts it.
- `stop_workspace`: stops a registered workspace by workspace ID or worktree path.
- `get_workspace_status`: returns a selected workspace status or all workspace statuses; use only for explicit status questions, already-running workspace inspection, or when `start_workspace` output is unavailable.
- `list_workspaces`: lists registered workspaces; use only when choosing among existing workspaces or answering an explicit list-workspaces question.
- `get_agent_up_context`: returns concise Agent-Up operating rules for AI agents.

`get_workspace_diagnostics` and `get_workspace_console` are documented on [Diagnostics](/developer-guide/diagnostics). `get_agent_up_json_format` is documented on [Configuration](/developer-guide/configuration).

MCP remains unauthenticated for local automation, but `/mcp` requests from a non-loopback remote address are hidden with a 404 response.

## Authentication and network boundaries

The REST API uses a single administrator account. Authentication is required by default for every REST endpoint unless the endpoint explicitly opts out. `AGENTUP_ADMIN_PASSWORD` supplies the administrator password for `POST /api/auth/login`, which returns an in-memory bearer token. `GET /api/auth/status` and the login endpoint are anonymous so clients can decide whether to display login. Bearer sessions expire after 24 hours by default; override with `AGENTUP_SESSION_LIFETIME_SECONDS`.

The Server starts even when `AGENTUP_ADMIN_PASSWORD` is unset. REST routes remain protected in that mode, but login cannot succeed until the password is configured.

For local development, copy `.env.example` to `.env` in the repository root. Server, Desktop, and CLI load that file on startup when present. Existing shell environment variables are not overridden.

Set `AGENTUP_AUTH_DISABLED=true` to run without REST authentication. Desktop and Mobile query authentication status and skip their login UI in that mode.

Desktop and Mobile reject remote `http://` Server URLs for administrator login and require HTTPS outside loopback hosts.

## Service hosting

Packaged installations run `AgentUp.Server` as the local `agent-up-server` service:

- macOS uses launchd.
- Windows uses Windows Service hosting.
- Ubuntu uses systemd.
- NixOS uses a systemd service definition through a Nix module.

Packaged services bind to `http://127.0.0.1:5000` by default. Service definitions that automatically restart the Server must throttle restart attempts to at least 5 seconds so a bind failure cannot create a tight restart loop.

`AgentUp.Tray` is the installed Server companion. It keeps a login autostart entry and heartbeats `POST /api/tray/heartbeat` so the Server knows a workstation session is present.

`POST /api/service/restart` and `POST /api/service/shutdown` are authenticated service-control routes for the packaged `agent-up-server` process. They are not workspace orchestration.

## Managed source clones

The `SourceClones` slice owns repositories Agent-Up clones for itself. `POST /api/source-clones` takes a repository and a branch, validates both, clones into the source clones root, and registers the resulting workspace through the `Workspaces` controller boundary. `GET /api/source-clones/root` reports the configured root.

The root comes from `AGENTUP_SOURCE_CLONES_ROOT` and otherwise defaults to a `sources` directory under the Server data directory. Remotes are restricted to `http`, `https`, `ssh`, and `git` URLs plus the `user@host:path` form. Local `file://` and transport-helper remotes are rejected.

`GET /api/workspaces/{workspaceId}/overview` returns workspace identity plus Server-measured worktree storage and local-process CPU and memory totals. Docker application containers are not included in the CPU and memory figures.

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts.

## Desktop and Mobile clients

Desktop displays workspaces, connects to one Server at a time, and may remember additional Server URLs with their login tokens. Switching Servers drops Desktop-local workspace and browser state. The workspace list `+` button posts to the Server's source-clones endpoint. Desktop does not clone, validate remotes, or choose a destination directory.

On first start, Desktop shows a required setup tutorial over the normal application shell unless the user has already completed or skipped it. Native Desktop E2E tests set `AGENTUP_SKIP_FIRST_RUN_TUTORIAL=1`. Installed Desktop artifacts connect to `http://localhost:5000` by default.

Mobile is an Expo client for Android, iOS, and the PWA. It displays Server-owned workspace state and submits requests. Route entrypoints stay under `src/app/`; product UI lives under `src/features/`. Remote servers must use HTTPS; loopback HTTP remains for local development. Run `./au-debug test mobile` before submitting mobile client changes. Maintainer visual comparison uses [`au-debug`](/developer-guide/repo/au-debug).
