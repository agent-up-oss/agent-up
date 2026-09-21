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
  tests="Server Agents HTTP smoke and capability Provider smoke"
  rest={'/api/workspaces/{workspaceId}/agent'}
/>

<DocSpine>
<DocBeat>Discover the inventory command</DocBeat>
<DocBeat>Complete subscription sign-in when ACP requires it</DocBeat>
<DocBeat>Stream prompts and permission decisions over REST/SSE</DocBeat>
</DocSpine>

<DocContract label="Route">POST .../authenticate</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab lists the workspace's saved sessions across Codex, Cursor, and Claude with agent, generated description, and last branch, then streams the selected session over SSE. User prompts are right-aligned catalog bubbles. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure.</DocSurface>
<DocSurface mobile>The Agents overview tab lists the workspace's saved sessions across all agent types with agent, generated description, and last branch. Selecting one resumes it and opens chat as an inner page. `session/request_permission` is a blocking decision card. The client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself.</DocSurface>
</DocSurfaces>

## Session runtime

The Server stores ACP session IDs and their display metadata under its data directory, partitioned by workspace ID. A generated ACP session title becomes the short description; the current workspace branch is recorded whenever a session is created, resumed, or retitled. Resuming uses ACP `session/load` in the workspace worktree, and deleting a workspace removes its saved session index.

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A second prompt is rejected while a turn is running rather than being executed concurrently. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Capability discovery

The Codex, Cursor, and Claude capability adapters discover installed ACP adapters from the command declared on that capability's inventory entry. Server and Desktop installments share `AGENTUP_CAPABILITY_INVENTORY_PATH`, `/etc/agent-up/capabilities.json`, `~/.config/agent-up/capabilities.local.json`, `~/.config/agent-up/capabilities.json`, and `.agent-up-dev/capabilities.json` found by walking up from the Server working directory. `command` may be a PATH name or a rooted path on disk; `arguments` and optional `versionArguments` travel with it. Adapters do not hardcode ACP executable names.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the Server data directory's `agent-cli-home`. Agent-Up does not collect API keys.

Live-CLI Provider smoke tests discover those installed executables, and Server Agents HTTP smoke asserts the workspace agent picker matches that discovery without skipping when a CLI is absent. `nix-shell shell.nix` writes a local inventory under `.agent-up-dev`.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

<DocNext href="/developer-guide/agents/sign-in" title="Sign-in">
Poll, code, and redirect transports.
</DocNext>
