---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace has one authenticated ACP session. The Server schedules the configured ACP executable in the worktree. Clients never launch the CLI or own the session.

A second agent is rejected until the current one is stopped. Prompts are serialized per workspace.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Agents surface"
  tests="Server Agents HTTP smoke and capability Provider smoke"
  rest={'/api/workspaces/{workspaceId}/agent'}
/>

<DocSpine>
<DocBeat>Enable the matching ACP package on the Server</DocBeat>
<DocBeat>Complete subscription sign-in when ACP requires it</DocBeat>
<DocBeat>Stream prompts and permission decisions over REST/SSE</DocBeat>
</DocSpine>

<DocContract label="Route">POST .../authenticate</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab streams workspace ACP events over SSE on a dedicated HTTP client with an infinite timeout. User prompts are right-aligned catalog bubbles. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure.</DocSurface>
<DocSurface mobile>The Agents overview tab lists Server-discovered ACP agents so the user can continue the current session or start a new one, then opens the existing chat module as an inner page. `session/request_permission` is a blocking decision card. The client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself.</DocSurface>
</DocSurfaces>

## Session runtime

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A second prompt is rejected while a turn is running rather than being executed concurrently. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Capability packages

Codex, Cursor, and Claude are first-party Agent SDK consumers that emit registry packages. The Server enables those packages, loads their DLLs, and lists ACP agents from enabled agent-kind modules by module id. Unknown enabled agent module ids are listed rather than skipped. Each launch uses `IAgentCapability.Launch` inside that agent package's Nix environment. Optional auth/login stays on the module. Desktop and Mobile list available agents from Server APIs. `nix-shell shell.nix` seeds a local enabled set under `.agent-up-dev` and caches the ACP CLIs there so a repository Server started outside that shell can still find them.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the Server data directory's `agent-cli-home`. Agent-Up does not collect API keys.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

<DocNext href="/developer-guide/agents/sign-in" title="Sign-in">
Poll, code, and redirect transports.
</DocNext>
