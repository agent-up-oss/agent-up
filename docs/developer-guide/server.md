---
title: Server
---

# AgentUp.Server

`AgentUp.Server` owns all runtime state and performs all orchestration.

## Workspace agents

The authenticated `/api/workspaces/{workspaceId}/agent` surface schedules one
ACP agent per workspace. Scheduling starts the configured ACP executable in the
workspace worktree, performs ACP `initialize` and `session/new`, and retains the
returned session ID. Prompts are serialized per workspace. A second agent is
rejected until the current one is stopped.

Message submission returns `202 Accepted` after the prompt has been handed to
the workspace session; the completed turn arrives over SSE. A second prompt is
rejected while a turn is running rather than being executed concurrently. Agent
failures become structured operation errors or state events, and stopping an
unresponsive adapter escalates from standard-input closure to process-tree
termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a
monotonic ID and the `after` query parameter replays retained events after a
disconnect. ACP `session/update` notifications are forwarded without discarding
their typed payload. `session/request_permission` requests are suspended until a
client posts the selected ACP option to `.../permissions`. Unsupported ACP
client-side requests fail explicitly rather than silently granting access.
Permission responses are accepted only when their request ID is pending and the
selected option was offered by that request. Slow SSE consumers are disconnected
instead of silently losing events, allowing them to reconnect with `after` and
replay from bounded Server history.

The Codex, Cursor, and Claude capability adapters discover installed ACP
adapters from the command declared on that capability's inventory entry. Server
and Desktop installments share `AGENTUP_CAPABILITY_INVENTORY_PATH`,
`/etc/agent-up/capabilities.json`, `~/.config/agent-up/capabilities.local.json`,
`~/.config/agent-up/capabilities.json`, and `.agent-up-dev/capabilities.json`
found by walking up from the Server working directory. Those files are merged
by capability id so a Home Manager version list does not hide ACP commands.
`command` may be a PATH name or a rooted path on disk; `arguments` and optional
`versionArguments` travel with it. Discovery then probes that declared command
on `PATH` and in well-known locations such as `~/.local/bin`. Adapters do not
hardcode ACP executable names. The interactive `codex` and `claude` CLIs are not
ACP servers, and installing the Cursor IDE does not install the Cursor Agent
CLI. Override executable names and argument arrays with
`Agents:<Codex|Cursor|Claude>:Command` and `Agents:<...>:Arguments`. Agent-Up does
not collect API keys. When ACP reports that authentication is required, the
session stays scheduled and `POST .../authenticate` starts that agent's
subscription login CLI instead of asking the ACP process to open a local
browser: `NO_OPEN_BROWSER=1 agent login` for Cursor, `codex login --device-auth`
for Codex, and `claude setup-token` for Claude. The Server streams the printed
sign-in URL and any device code to Desktop and Mobile, waits until the CLI
exits, then restarts ACP against the Server data directory's `agent-cli-home`
so the subscription session survives process restart. Override the login
executable with `Agents:<Kind>:LoginCommand` and `LoginArguments`. An agent
is shown as available when its capability adapter discovers the inventory
command, or when `Agents:<Kind>:Command` points at an existing executable.
Live-CLI Provider smoke tests discover those installed executables, and Server
Agents HTTP smoke asserts the workspace agent picker matches that discovery
without skipping when a CLI is absent. `nix-shell shell.nix` writes a local
inventory under `.agent-up-dev` so developer workstations can exercise the
adapters without a packaged installment.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP
client cannot safely proxy an interactive terminal. ACP `authenticate` is not
used for remote subscription login because Cursor, Codex, and Claude implement
that as `xdg-open` on the Server host. Clients display advertised subscription
methods (API-key methods are hidden), and `POST .../authenticate` runs the
vendor CLI described above. Codex ChatGPT device-code login must be enabled in
ChatGPT security settings. Claude `setup-token` stores the resulting
`sk-ant-oat` subscription token under the Server data directory; it is not an
Anthropic console API key.

## Responsibilities

The Server manages:

- Workspace registry.
- Managed source clones.
- Git working-tree review and commits.
- Process lifecycle.
- Port allocation.
- Authenticated HTTPS forwarding of allocated HTTP application ports.
- Hosted Linux desktop application sessions.
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

Allocated HTTP application ports stay bound on the Server host. Remote clients
reach them through `POST /api/apps/tickets` and the `/apps/{workspaceId}/{port}`
bootstrap, which sets an HttpOnly cookie and reverse-proxies unmatched paths
to `http://127.0.0.1:{port}`. Bootstrap redirects stay on the Server origin.
Unsafe proxied writes require an Origin that matches the Server scheme, host,
and port. Abandoned tickets expire after 30 seconds and are evicted on later
issue or consume. That cookie does not authorize REST or MCP routes. Only
currently listening allocated HTTP ports are forwarded.

The REST API permits cross-origin browser requests from any HTTP or HTTPS
origin, so the Mobile web/PWA client can reach a Server the user points it at
regardless of where that client is hosted (a local dev port, an installed
PWA, or a deployed preview build). The browser's own mixed-content policy
still applies: a client served over HTTPS cannot fetch a plain-HTTP Server
unless that Server is loopback-hosted, so a remote Server should be reachable
over HTTPS.

A clustered Server is published as `docker.io/themassiveone/agent-up-server` and installed from the `agent-up-helm` chart. That path is an installation concern only. The image includes `git` for source clones and Git review, plus the Codex, Cursor, and Claude ACP CLIs under `/opt/agent-up/bin`. Chart defaults enable those three ACP capabilities with rooted `command` paths so the agent picker can discover them after install.

This service shape is packaging and lifecycle behavior only. Runtime ownership remains unchanged: all orchestration stays in `AgentUp.Server`, and Desktop stays a client.

This rule keeps concurrent agents, human developers, and automation clients aligned around the same running environment.

## Orchestration Rule

If a feature starts, stops, restarts, navigates, records, allocates, diagnoses, or exports workspace behavior, that logic belongs in the Server.

## Process Environment

Desktop applications are prepared before their process starts. The `DesktopApplications` slice starts a dedicated Xvfb display, creates a private `XDG_RUNTIME_DIR` for that session, and injects `DISPLAY` together with `GDK_BACKEND=x11`, `WAYLAND_DISPLAY=agentup-hosted-no-wayland`, `XDG_SESSION_TYPE=x11`, `GTK_USE_PORTAL=0`, `QT_QPA_PLATFORM=xcb`, and software-GL variables so toolkit autodetect cannot attach to the host Wayland or X11 session. It also injects `LD_LIBRARY_PATH`, merging the Server process path with libraries imported from a nearby `shell.nix` when Nix is present, so SkiaSharp native dependencies such as fontconfig resolve even if the Server was started from an IDE without `nix-shell`. It captures the root framebuffer as PNG and tears the display down with the application or workspace. Sessions have monotonically changing generations so delayed input from a previous process is rejected. Raw X11 sockets are never exposed; clients enter through authenticated ticket issuance and the ticket-scoped viewer/WebSocket routes.

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

## Managed Source Clones

The `SourceClones` slice owns repositories Agent-Up clones for itself, as opposed to worktrees registered from the CLI or MCP. `POST /api/source-clones` takes a repository and a branch, validates both, clones into the source clones root, and registers the resulting workspace through the `Workspaces` controller boundary. `GET /api/source-clones/root` reports the configured root.

The root comes from `AGENTUP_SOURCE_CLONES_ROOT` and otherwise defaults to a `sources` directory under the Server data directory. The slice resolves a destination directory from the repository name, verifies it stays under that root, and refuses a clone when the destination already exists.

Remotes are restricted to `http`, `https`, `ssh`, and `git` URLs plus the `user@host:path` form. Local `file://` and transport-helper remotes are rejected so a REST caller cannot make the Server read arbitrary local repositories. Branch names are validated against Git ref rules before any process starts, and the clone runs with `GIT_TERMINAL_PROMPT=0` so a credential prompt cannot hang the Server.

Registration prefers the repository's own `agent-up.json` through the `Orchestration` registration controller. A repository without that file still registers, using the clone directory name and the identity read from the new checkout.

## Workspace Overview

`GET /api/workspaces/{workspaceId}/overview` returns workspace identity plus Server-measured worktree storage and local-process CPU and memory totals for the Desktop Overview tab. Docker application containers are not included in the CPU and memory figures.

## Git Working Tree

The `Git` slice is the Server-side capability behind the Desktop Git panel and the Mobile Git tab. It resolves the selected workspace's worktree path and exposes these routes:

- `GET /api/workspaces/{workspaceId}/git/changes` returns the uncommitted changes as a directory tree with per-file status, the live branch name, and local branch names.
- `GET /api/workspaces/{workspaceId}/git/head` returns the live branch name and local branch names without the change tree.
- `GET /api/workspaces/{workspaceId}/git/file?path=` returns one file's diff, including untracked files.
- `POST /api/workspaces/{workspaceId}/git/commit` stages and commits only the requested paths with the supplied message and returns the new commit.
- `POST /api/workspaces/{workspaceId}/git/discard` restores selected tracked files from HEAD and deletes selected untracked files.
- `POST /api/workspaces/{workspaceId}/git/branch` switches to a local branch or creates a new branch from the current HEAD.

The provider runs Git through an allowlisted operation set with `ProcessStartInfo.ArgumentList`, rejects pathspec magic, option-shaped paths, and paths that resolve outside the repository root, and always passes `--` before user-supplied paths. Commit uses `git commit --only` after staging the selected files that still exist. New files that vanished after they were staged are unstaged instead of failing the whole commit. Because the commit passes explicit pathspecs, changes to files the caller did not select stay in the worktree.

This slice is separate from the `Commits` slice. `Commits` owns the agent-facing commit queue, which stages vertical slices for a developer to review. `Git` owns the human review-and-commit surface in Desktop and Mobile.

## Tutorial Cleanup

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts, so stale workspace state cannot render behind onboarding or affect the guided sample setup.
