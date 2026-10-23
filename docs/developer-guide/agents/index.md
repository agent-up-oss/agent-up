---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace has one active authenticated ACP session and a Server-persisted history of that workspace's sessions. The Server schedules the configured ACP executable in the worktree. Clients never launch the CLI or own the session.

Starting or resuming a session replaces the active process without mixing histories between workspaces. Prompts are serialized per workspace.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Agents surface"
  tests="Server Agents HTTP smoke, loop soak, capability Provider smoke"
  rest={'/api/workspaces/{workspaceId}/agent · /api/autonomy'}
/>

<DocSpine>
<DocBeat>Enable the matching ACP package on the Server</DocBeat>
<DocBeat>Complete subscription sign-in when ACP requires it</DocBeat>
<DocBeat>Stream prompts and permission decisions over REST/SSE</DocBeat>
</DocSpine>

<DocContract label="Route">POST .../authenticate</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab lists the workspace's saved sessions across Codex, Cursor, and Claude with agent, generated description, and last branch. The live session is labeled Current; every other row is Resume. Empty history says there are no saved sessions yet. Paste-code sign-in collects the authorization code in the client and posts it to the Server. User prompts are right-aligned catalog bubbles. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure. Loop phase, task, and stop reason render under the status line. ACP `Authentication required` is Waiting for sign-in, not a failed-session banner.</DocSurface>
<DocSurface mobile>The Agents overview tab lists the workspace's saved sessions across all agent types with agent, generated description, and last branch. The live session is labeled Current; every other row is Resume. A bound workspace hides the picker and shows that agent. Selecting a session resumes it and opens chat as an inner page. `session/request_permission` is a blocking decision card. The client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself. ACP `Authentication required` is Waiting for sign-in, not a raw JSON-RPC error.</DocSurface>
</DocSurfaces>

## Session runtime

The Server stores ACP session IDs and their display metadata under its data directory, partitioned by workspace ID. A generated ACP session title becomes the short description; the current workspace branch is recorded whenever a session is created, resumed, or retitled. Resuming uses ACP `session/load` in the workspace worktree, and deleting a workspace removes its saved session index.

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A second prompt is rejected while a turn is running rather than being executed concurrently. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Capability packages

Codex, Cursor, and Claude are first-party Agent SDK consumers that emit registry packages. The Server enables those packages, loads their DLLs, and lists those first-party agents even when a package is off, plus any other enabled agent-kind module by module id. Unknown enabled agent module ids are listed rather than skipped. `GET /api/workspaces/{id}/agent` descriptors carry `unavailability` (`capabilityNotEnabled` or `executableNotFound`), `detail`, and `remedy` when `available` is false, so Desktop and Mobile can render the cause and Enable the package from the row. Each launch uses `IAgentCapability.Launch` inside that agent package's Nix environment. Optional auth/login stays on the module. Desktop and Mobile list available agents from Server APIs. A Development or Testing Server also lists `test-agent-scripted` so the Agent tab can start a session without a vendor CLI. `nix-shell shell.nix` seeds a local enabled set under `.agent-up-dev` and caches the ACP CLIs there so a repository Server started outside that shell can still find them. Runtime capability E2E sets `AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP=1` so that hook does not download those CLIs; the job packs the packages and Desktop enables only `docker` and `dotnet`.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the Server data directory's `agent-cli-home`. The ACP JSON-RPC `Authentication required` / `-32000` notice is a sign-in prompt, not a failed session. A pasted-code sign-in that finishes without a captured subscription token returns to authentication instead of starting unsigned. Agent-Up does not collect API keys.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

## Workspace bind and unattended loop

A workspace may record one bound agent module id. Unbound is the interactive picker. Autonomy stays off until the Server is enabled and the workspace is activated against a policy, a host connection, and assigned issues. Roles prefix the prompt and may filter advertised MCP tools. The full contract is on Autonomy.

## Loop coverage

CI runs the Server loop kernel against a locally-inferring ACP test agent on CPU llama.cpp. Hard invariants fail the run. Pooled task-completion outcomes are compared to a versioned baseline. Scenario files under `loop-coverage/` do not name an agent, so the same prompts can drive CPU inference and `test-agent-scripted`. Concurrency and restart for at least three autonomous workspaces stay in the normal E2E tier. A separate soak check drains hundreds of scripted tasks.

<DocNext href="/developer-guide/agents/autonomy" title="Autonomy">
Bind, policy, unattended loop, roles, scripted ACP, and soak.
</DocNext>
