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
Use `start_workspace` immediately when users ask to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up. A client sharing the Server's host passes the absolute repository/worktree path and must not call `list_workspaces` or `get_workspace_status` first. A client that reached the Server across the network selects a workspace id through `list_workspaces` instead, because it cannot name a path the Server can open. Agent-Up starts the development environment the Server hosts; it does not deploy to cloud infrastructure.
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
Takes exactly one of `workspaceId` or `worktreePath`. `worktreePath` registers or updates a workspace from its `agent-up.json` and then starts it; if the file is missing, it tells the agent to read the user configuration guide, search for an existing file, or ask before creating one. `workspaceId` starts a workspace the Server already holds, so a client with no shared filesystem never needs a path. Passing both, or neither, is a validation error, and a bound session passes neither.
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

## Session instructions

`AgentUpMcpGuidance` builds the instructions a client reads on initialize, and `McpEndpointSessionProvider.ConfigureAsync` picks the variant from the session. Only the workspace-addressing rule changes; the validation feedback loop and the commit-queue discipline are the same everywhere.

<DocFacts label="Variants">
<DocFact label="Shared filesystem">Loopback caller: register the current repository/worktree and pass its absolute path to `start_workspace`.</DocFact>
<DocFact label="Remote">Non-loopback caller: no shared filesystem, so select a workspace id through `list_workspaces`. Any path a tool returns is a location on the Server's host.</DocFact>
<DocFact label="Pinned">Token with a `workspace` claim: the session already targets one workspace and has no selection step.</DocFact>
</DocFacts>

Per-endpoint servers replace the body with their own slice text and keep the same addressing rule appended, so a commits-only or browser-only client is never told a different answer to the filesystem question.

## Permission-scoped toolsets

`McpToolPermissionMap` declares the `OperationPermissions` constant every MCP tool requires, reusing the same permission names REST endpoints do. A session whose token carries `permissions` claims is advertised only the tools those claims allow, and a call that names a removed tool is refused with the missing permission. A caller with no `permissions` claim — loopback anonymous, local administrator, an auth-disabled Server — keeps the full per-endpoint toolset.

The per-endpoint allowlist, the permission filter, and the workspace pin apply in that order and each only removes, so none of them widens another. `AgentUp.Architecture.Tests` fails when a declared tool has no permission entry or an entry names no declared tool.

## Remote MCP access

Loopback MCP is anonymous and stays that way. Every other remote address is hidden with a 404 response unless `AGENTUP_MCP_REMOTE_ENABLED=true`, which also requires `externalBearer` so there is a token to verify.

<DocCallout kind="warning">
A remote caller with no token, or one that fails signature, expiry, or audience validation, is unauthenticated and still gets 404: the Server must not advertise MCP to a caller who cannot use it. A verified caller missing the endpoint's permission floor gets 403. A presented bearer token that carries a `workspace` claim is still pinned for that MCP session.
</DocCallout>

`McpEndpointPermissions` holds the floor per endpoint — the lowest permission any tool on that endpoint needs: `browser.control` for `/mcp/browser`, `git.read` for `/mcp/commits` and `/mcp/verification`, `workspace.read` for `/mcp/orchestration`, `diagnostics.read` for `/mcp/audit`, and `server.read` for `/mcp/capabilities`. Individual tools may still require more, through [Permission-scoped toolsets](#permission-scoped-toolsets). `McpNetworkRestrictionMiddleware` runs after `UseAuthentication` so the caller is already identified when the 404-or-continue decision is made, and loopback anonymity survives `RequireAuthorization` through `McpLoopbackOrPermissionRequirement` rather than by skipping authorization.

With remote access enabled and `AGENTUP_EXTERNAL_ISSUER` set, `/.well-known/oauth-protected-resource` serves RFC 9728 protected resource metadata. Agent-Up is the resource server, so the only authorization server the document names is that issuer, and the resource identifier is derived from the request rather than from a configured hostname. Leave either setting out and the document is absent.

## Authentication and network boundaries

The REST API does not require authentication until `AGENTUP_ADMIN_PASSWORD` or `AGENTUP_AUTH_MODE` selects a credential mode. `GET /api/auth/status` and `POST /api/auth/login` are anonymous so Desktop, Mobile, and other clients can decide whether to display sign-in.

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

- `localAdministrator`: selected when `AGENTUP_ADMIN_PASSWORD` is set, or when `AGENTUP_AUTH_MODE=localAdministrator`. `POST /api/auth/login` returns an in-memory bearer token.
- `externalBearer`: login is rejected. Present a signed JWT whose `iss` and `aud` match `AGENTUP_EXTERNAL_ISSUER` and `AGENTUP_EXTERNAL_AUDIENCE`. Configure exactly one verification source: an HMAC secret in `AGENTUP_EXTERNAL_SIGNING_KEY`, a PEM-encoded RSA or EC public key in `AGENTUP_EXTERNAL_PUBLIC_KEY`, or an absolute HTTPS JSON Web Key Set endpoint in `AGENTUP_EXTERNAL_JWKS_URI`. `AGENTUP_EXTERNAL_ALGORITHMS` may set a comma-separated signing-algorithm allowlist; `none` is forbidden. Otherwise the selected source defaults to HS256, RS256, ES256, or both RS256 and ES256 for JWKS. JWKS keys are cached for five minutes; expired keys fail closed, fetches time out after five seconds, and all refresh attempts are limited to once per minute so issuer key rotation does not enable outbound request amplification. Optional claims: `workspace`, `tenant`, and repeated `permissions` values. A `workspace` claim binds the caller to that workspace id on REST routes and on MCP tool calls. Bound MCP sessions omit every workspace target from advertised tool schemas — workspace id parameters and the `worktreePath` and `repositoryPath` path parameters alike — and refuse `workspaceId`, workspace `id`, `worktreePath`, and `repositoryPath` arguments that name another workspace, including the managed proposal-queue worktree of a different workspace. The Server supplies the bound target instead: the bound id for every id parameter, and the bound workspace's worktree path for a tool whose only target is a path. A path argument a bound caller sent anyway is checked against the bound workspace and then dropped, so it can neither reach a tool as a second target nor act as a filter the schema no longer offers. Protected operations require the matching `permissions` claim; a token with none can still read `GET /api/entitlements`. On MCP, `permissions` claims also scope the advertised toolset, as described under [Permission-scoped toolsets](#permission-scoped-toolsets).
- `disabled` (default): REST authentication is off. Also selected by `AGENTUP_AUTH_MODE=disabled` or `AGENTUP_AUTH_DISABLED=true`, including when a password is configured.

`GET /api/connection` returns anonymous connection metadata: `apiVersion` (`1`), `connectionId`, `kind` (`selfHosted`), `displayName`, `workspacePresentation` (`serverScoped`), and `authentication` (`mode`, `prompt`, `identifierRequired`). Clients populate a shared `ConnectionSource` from that document and choose sign-in UI from `authentication.mode`: `localAdministrator` is a password form, `externalBearer` expects an issued credential, `browserSso` opens the server's `/api/auth/sso` start URL, and `disabled` connects without a prompt. An unknown `apiVersion`, `kind`, or `authentication.mode` is a hard error. A Server that does not answer `GET /api/connection` is treated as legacy self-hosted only after `GET /api/auth/status` succeeds; a malformed 200 is not legacy. The OSS Server never emits `browserSso`. `Examples/browser-sso` is a runnable identity front door that does, so Mobile can be exercised against that contract. `identifierRequired` is unused.

Clients key WebView tabs, Git panel state, SSE cursors, and in-flight request gates on `(saved connection id, workspace id)` so two Servers that both expose a workspace called `main` do not share cache entries.

`GET /api/entitlements` returns the authenticated permission document. Feature keys are operation permissions (`workspace.read`, `agent.prompt`, `git.write`, and the rest of the Server operation set). A self-hosted Server always returns `source: selfHosted`, `edition: community`, `billing: free`, and every operation `available: true`. Clients render this document as one plan card driven by `features` and `limits`; they must not branch on edition names.

When no authentication environment variable is set, REST routes are anonymous and `GET /api/connection` reports `disabled`. Desktop, Mobile, CLI, and Tray follow that document: they prompt only when the Server requires a credential.

For local development, copy `.env.example` to `.env` in the repository root. Server, Desktop, and CLI load that file on startup when present. Existing shell environment variables are not overridden.

Set `AGENTUP_ADMIN_PASSWORD` or `AGENTUP_AUTH_MODE=externalBearer` to require REST authentication. `AGENTUP_AUTH_DISABLED=true` turns it off again. Desktop and Mobile query authentication status and skip their login UI when the Server reports `disabled`.

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

`POST /api/workspaces/tutorial/cleanup` stops and removes every registered workspace. Desktop and Mobile onboarding is the built-in Demo server, not this endpoint. The Desktop FirstRun tutorial overlay is gone; every launch is the server picker.

## Desktop and Mobile clients

Desktop displays workspaces, connects to one Server at a time, and may remember additional Server URLs with their login tokens. Switching Servers drops Desktop-local workspace and browser state. The workspace list `+` button posts to the Server's source-clones endpoint. Desktop does not clone, validate remotes, or choose a destination directory.

Desktop and Mobile always list a built-in **Demo** server (`http://127.0.0.1:9`, display name `Demo`) on the connect picker. When `AGENTUP_RECOMMENDED_SERVER_URL` or `EXPO_PUBLIC_RECOMMENDED_SERVER_URL` is set, that connection is listed after Demo and cannot be removed. Desktop opens that picker on every launch before it probes a packaged or repository Server URL, so Demo can be selected without a running Server. Connecting to Demo switches the client onto an in-process fake backend defined by `AgentUp.FakeServer/definition.json` and shipped inside both executables. Real API clients stay unchanged: Desktop intercepts `HttpClient` with `FakeServerMessageHandler`, and Mobile intercepts `fetch`. Only while Demo is the active connection is app state faked; disconnecting and connecting to a real Server works without restart. Application WebViews cannot use that interceptor, so Desktop writes bundled HTML to a temp `file://` page and Mobile loads `html` / `srcDoc`. The same definition drives Git mutations, first-party capability modules, and workspace start/stop phases on both clients. Desktop hides Database, Diagnostics, and Metrics while Demo is active. The Validation sidebar appears only when an application is selected and starts collapsed. Installed Desktop artifacts connect to `http://localhost:5000` by default after a real Server is chosen.

Mobile displays Server-owned workspace state and submits requests. The sidebar Server section shows the current connection and offers Logout, which returns to the connect screen where saved servers can be switched or added. Workspace chrome is Apps, Git, Agents, and Settings; Settings hosts capability modules. It subscribes to `GET /api/workspaces/events` so Apps-tab start/stop controls and status LEDs follow Server lifecycle and port health. Route entrypoints stay under `src/app/`; product UI lives under `src/features/`. Remote servers must use HTTPS; loopback HTTP remains for local development. When `EXPO_PUBLIC_RECOMMENDED_SERVER_URL` or `AGENTUP_RECOMMENDED_SERVER_URL` is set, that connection is listed after Demo and cannot be removed. Sign-in UI is chosen from `GET /api/connection` `authentication.mode`, not from hostname or build flags. The workspace list renders `GET /api/entitlements` as one plan card of features and limits and hides Add workspace when `workspace.create` is unavailable. Run `./au-debug test mobile` before submitting mobile client changes. Maintainer visual comparison uses [`au-debug`](/developer-guide/repo/au-debug).

<DocNext href="/developer-guide/workspaces/workflows" title="Workflows">
Modify, restart, inspect, validate.
</DocNext>
