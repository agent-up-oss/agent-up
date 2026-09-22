---
title: Workspaces
---

<DocEyebrow slice="Workspaces" status="available" />

# Workspaces

<DocWhat>
A workspace is the unit of isolation. `AgentUp.Server` owns identity, start/stop, clones, and login. Desktop, Mobile, CLI, and MCP clients display that state and request actions. They do not keep authoritative copies.

Packaged installations run `agent-up-server` on `http://localhost:5000`; the repository launch profile uses `http://localhost:5001`.
</DocWhat>

<DocMeta
  owner="AgentUp.Server"
  tests="AgentUp.Server.Tests/Features/Workspaces/ and SourceClones/"
  mcp="/mcp/orchestration"
  rest="/api/workspaces, /api/workspaces/events, /api/source-clones, /api/auth, /api/connection, /api/entitlements"
/>

<DocSpine>
<DocBeat>Register or start from `agent-up.json`</DocBeat>
<DocBeat>Use the returned workspace id</DocBeat>
<DocBeat>Inspect status only when the user asked</DocBeat>
</DocSpine>

<DocContract label="Tool">start_workspace</DocContract>

<DocCallout>
Use `start_workspace` immediately when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up. Do not call `list_workspaces` or `get_workspace_status` first when the current repository/worktree is known. Agent-Up starts local development environments; it does not deploy to cloud infrastructure.
</DocCallout>

## Orchestration MCP

`/mcp/orchestration` exposes Streamable HTTP and legacy SSE at `/mcp/orchestration/sse` plus `/mcp/orchestration/message`. It owns workspace tools, workspace resources, and Agent-Up context resources.

<DocFacts label="Resources">
<DocFact label="context">agent-up://context</DocFact>
<DocFact label="schema">agent-up://agent-up-json</DocFact>
<DocFact label="list">agent-up://workspaces</DocFact>
<DocFact label="one">{'agent-up://workspaces/{id}'}</DocFact>
</DocFacts>

<DocSteps>
<DocStep title="start_workspace">
Registers or updates a workspace from its `agent-up.json`, then starts it. If the file is missing, it tells the agent to read the user configuration guide, search for an existing file, or ask before creating one.
</DocStep>
<DocStep title="stop_workspace">
Stops a registered workspace by workspace ID or worktree path.
</DocStep>
<DocStep title="get_workspace_status">
Returns a selected workspace status or all workspace statuses. Use only for explicit status questions, already-running workspace inspection, or when `start_workspace` output is unavailable.
</DocStep>
<DocStep title="list_workspaces">
Lists registered workspaces. Use only when choosing among existing workspaces or answering an explicit list-workspaces question.
</DocStep>
<DocStep title="get_agent_up_context">
Returns concise Agent-Up operating rules for AI agents.
</DocStep>
</DocSteps>

`get_workspace_diagnostics` and `get_workspace_console` are documented on [Diagnostics](/developer-guide/diagnostics). `get_agent_up_json_format` is documented on [Configuration](/developer-guide/configuration).

<DocCallout kind="warning">
MCP remains unauthenticated for local automation, but `/mcp` requests from a non-loopback remote address are hidden with a 404 response.
</DocCallout>

## Authentication and network boundaries

The REST API uses authentication by default for every REST endpoint unless the endpoint explicitly opts out. `GET /api/auth/status` and `POST /api/auth/login` are anonymous so Desktop, Mobile, and other clients can decide whether to display sign-in.

<DocFacts>
<DocFact label="Password">AGENTUP_ADMIN_PASSWORD</DocFact>
<DocFact label="Mode">AGENTUP_AUTH_MODE</DocFact>
<DocFact label="Login">POST /api/auth/login</DocFact>
<DocFact label="Status">GET /api/auth/status (anonymous)</DocFact>
<DocFact label="Connection">GET /api/connection (anonymous)</DocFact>
<DocFact label="Entitlements">GET /api/entitlements</DocFact>
<DocFact label="Lifetime">24 hours, or AGENTUP_SESSION_LIFETIME_SECONDS</DocFact>
</DocFacts>

`AGENTUP_AUTH_MODE` selects how credentials are validated:

- `localAdministrator` (default): `AGENTUP_ADMIN_PASSWORD` for `POST /api/auth/login`, which returns an in-memory bearer token.
- `externalBearer`: login is rejected. Present a signed JWT whose `iss`, `aud`, and HMAC key match `AGENTUP_EXTERNAL_ISSUER`, `AGENTUP_EXTERNAL_AUDIENCE`, and `AGENTUP_EXTERNAL_SIGNING_KEY`. Optional claims: `workspace`, `tenant`, and repeated `permissions` values. A `workspace` claim binds the caller to that workspace id. Protected operations require the matching `permissions` claim; a token with none can still read `GET /api/entitlements`.
- `disabled`, or `AGENTUP_AUTH_DISABLED=true`: REST authentication is off.

`GET /api/connection` returns anonymous connection metadata: `kind` (`selfHosted`), `workspacePresentation` (`serverScoped`), and `authentication` (`mode`, `prompt`, `identifierRequired`). Clients use `mode` and `prompt` to render sign-in; they must not infer capabilities from `kind`. `localAdministrator` is a password form. `browserSso` opens the server's `/api/auth/sso` start URL in a browser. The OSS Server never emits `browserSso`. `Examples/browser-sso` is a runnable identity front door that does, so Mobile can be exercised against that contract. `identifierRequired` is unused.

`GET /api/entitlements` returns the authenticated permission document. Feature keys are operation permissions (`workspace.read`, `agent.prompt`, `git.write`, and the rest of the Server operation set). A self-hosted Server always returns `source: selfHosted`, `edition: community`, `billing: free`, and every operation `available: true`. Clients render this document; they must not branch on edition names.

The Server starts even when `AGENTUP_ADMIN_PASSWORD` is unset. REST routes remain protected in that mode, but local administrator login cannot succeed until the password is configured.

For local development, copy `.env.example` to `.env` in the repository root. Server, Desktop, and CLI load that file on startup when present. Existing shell environment variables are not overridden.

Set `AGENTUP_AUTH_DISABLED=true` to run without REST authentication. Desktop and Mobile query authentication status and skip their login UI in that mode.

Desktop and Mobile reject remote `http://` Server URLs for administrator login and require HTTPS outside loopback hosts.

## Service hosting

Packaged installations run `AgentUp.Server` as the local `agent-up-server` service.

<DocFacts label="Adapters">
<DocFact label="macOS">launchd</DocFact>
<DocFact label="Windows">Windows Service</DocFact>
<DocFact label="Ubuntu">systemd</DocFact>
<DocFact label="NixOS">systemd through a Nix module</DocFact>
</DocFacts>

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

Desktop and Mobile always list a built-in **Demo** server (`http://127.0.0.1:9`, display name `Demo`). Connecting to it switches the client onto an in-process fake backend defined by `AgentUp.FakeServer/definition.json` and shipped inside both executables. Real API clients stay unchanged: Desktop intercepts `HttpClient` with `FakeServerMessageHandler`, and Mobile intercepts `fetch`. Only while Demo is the active connection is app state faked; disconnecting and connecting to a real Server works without restart. Application WebViews cannot use that interceptor, so Desktop writes bundled HTML to a temp `file://` page and Mobile loads `html` / `srcDoc`. Installed Desktop artifacts connect to `http://localhost:5000` by default.

Mobile displays Server-owned workspace state and submits requests. It subscribes to `GET /api/workspaces/events` so Apps-tab start/stop controls and status LEDs follow Server lifecycle and port health. Route entrypoints stay under `src/app/`; product UI lives under `src/features/`. Remote servers must use HTTPS; loopback HTTP remains for local development. When `EXPO_PUBLIC_RECOMMENDED_SERVER_URL` or `AGENTUP_RECOMMENDED_SERVER_URL` is set, that connection is always listed first and cannot be removed. If login is required, Mobile reads `GET /api/connection` for the sign-in prompt and whether to open `/api/auth/sso` or the password form. The workspace list renders `GET /api/entitlements` as an edition card keyed by operation permissions and hides Add workspace when `workspace.create` is unavailable. Run `./au-debug test mobile` before submitting mobile client changes. Maintainer visual comparison uses [`au-debug`](/developer-guide/repo/au-debug).

<DocNext href="/developer-guide/workspaces/workflows" title="Workflows">
Modify, restart, inspect, validate.
</DocNext>
