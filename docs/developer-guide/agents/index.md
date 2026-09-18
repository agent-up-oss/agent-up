---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocFocus>
One authenticated ACP session per workspace. Clients never launch the CLI or own the session.
</DocFocus>

**Owner:** `AgentUp.Server` Agents surface. Tests live in Server Agents HTTP smoke and capability Provider smoke. REST: `/api/workspaces/{workspaceId}/agent`. Sign-in transports: [Sign-in](/developer-guide/agents/sign-in).

## What it is

The authenticated `/api/workspaces/{workspaceId}/agent` surface schedules one ACP agent per workspace. Scheduling starts the configured ACP executable in the workspace worktree, performs ACP `initialize` and `session/new`, and retains the returned session ID. Prompts are serialized per workspace. A second agent is rejected until the current one is stopped.

<DocSpine>
<DocBeat selected>Discover the inventory command</DocBeat>
<DocBeat>Complete subscription sign-in when ACP requires it</DocBeat>
<DocBeat>Stream prompts and permission decisions over REST/SSE</DocBeat>
</DocSpine>

<DocContract>POST .../authenticate</DocContract>

Next in this slice: [Sign-in](/developer-guide/agents/sign-in).

## Session runtime

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A second prompt is rejected while a turn is running rather than being executed concurrently. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Capability discovery

The Codex, Cursor, and Claude capability adapters discover installed ACP adapters from the command declared on that capability's inventory entry. Server and Desktop installments share `AGENTUP_CAPABILITY_INVENTORY_PATH`, `/etc/agent-up/capabilities.json`, `~/.config/agent-up/capabilities.local.json`, `~/.config/agent-up/capabilities.json`, and `.agent-up-dev/capabilities.json` found by walking up from the Server working directory. `command` may be a PATH name or a rooted path on disk; `arguments` and optional `versionArguments` travel with it. Adapters do not hardcode ACP executable names.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the Server data directory's `agent-cli-home`. Agent-Up does not collect API keys.

Live-CLI Provider smoke tests discover those installed executables, and Server Agents HTTP smoke asserts the workspace agent picker matches that discovery without skipping when a CLI is absent. `nix-shell shell.nix` writes a local inventory under `.agent-up-dev`.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

## Desktop and Mobile chrome

<DocSurface desktop>The Agent tab streams workspace ACP events over SSE on a dedicated HTTP client with an infinite timeout. User prompts are right-aligned catalog bubbles. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure.</DocSurface>

<DocSurface mobile>The Agents overview tab lists Server-discovered ACP agents so the user can continue the current session or start a new one, then opens the existing chat module as an inner page. `session/request_permission` is a blocking decision card. Subscription login is a Server-owned CLI flow: the client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself.</DocSurface>
