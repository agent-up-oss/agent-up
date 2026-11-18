---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace keeps its own saved ACP sessions and has one active session at a time. Sign in with that agent's subscription, never an API key.

Desktop chrome for the live session is the **Agent** tab. Mobile chrome for the picker is the **Agents** tab. Agent-Up does not collect API keys.
</DocWhat>

<DocSpine>
<DocBeat>Pick an available agent to bind it to the workspace</DocBeat>
<DocBeat>Complete subscription sign-in if the agent asks</DocBeat>
<DocBeat>Prompt in the live session, or watch an unattended loop</DocBeat>
</DocSpine>

<DocContract label="Chrome">Desktop Chat · Mobile Agents</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab lists saved sessions and streams the selected workspace session. The live session is labeled Current; every other row is Resume. Once an agent is bound, the picker hides and the tab shows that agent. When a pasted-code sign-in is waiting, the tab collects the code and sends it to the Server. The agent picker, saved sessions, and the unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal are on the workspace **Settings** tab; the Agent tab keeps one summary line with a **Settings** button. A message sent while the agent is working steers its next turn instead of stopping it, and is marked **Steering · next turn**. Replies are formatted text with code blocks you can copy, and each tool the agent used is one line you can open to see what went in and what came back.</DocSurface>
<DocSurface mobile>Agents shows the workspace's bound agent and opens its chat as an inner page. An unbound workspace shows a + action whose creation page lists agent capabilities already enabled on the Server. The unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal are on the workspace Settings tab; Agents shows one summary line that opens it. Chat scrolls as one column, and the message box follows the transcript. While the agent is working, **Send** becomes **Steer**: the message is read on the next turn. The tab bar stays.</DocSurface>
</DocSurfaces>

<p><img src="/desktop-agents.png" alt="Desktop Agent tab showing the agent bound to Bob's workspace" /></p>
<p><img src="/mobile-agents.png" alt="Mobile Agents tab showing Codex" /></p>

## Workspace agent CLIs

Saved sessions from every enabled ACP agent appear together within the current workspace. Each row shows the agent, the agent-generated short description, and the branch last associated with the session. The live session is labeled Current. Select any other row to resume it. Sessions from another workspace never appear in this list.

Workspace agent chat uses the ACP agents built into the Server: Codex, Cursor, Claude, and OpenCode. The picker lists each one, enabled or not. The Server wraps those launches in Nix. Enable an agent from its row, Desktop capability chrome, or Mobile Settings. An agent whose software the Server cannot find cannot be enabled.

## OpenCode

OpenCode runs against a model served by an Ollama-compatible API instead of a subscription, so it has no sign-in. Once it is enabled, its Settings card expands with three fields:

- **Endpoint** — the API's base URL, such as `http://localhost:11434`. A bare host gets `/v1` added; an endpoint that already names a path is used as written.
- **Token** — optional, for a hosted endpoint. It is stored on the Server and never shown again; leave the field empty to keep it.
- **Model** — the model name the endpoint serves, such as `qwen2.5-coder:7b`.

Agent-Up gives OpenCode its own tools - workspaces, commits, verification, the browser, diagnostics, and decisions - so it can start and inspect the workspace it works in.

Until Endpoint and Model are set, the OpenCode row reads **Needs setup**. OpenCode asks before every edit and command, so it can be bound as an author or a reviewer and run unattended under a permission policy like the other agents.

## Subscription sign-in

Sign in with the corresponding CLI's subscription login from Desktop or Mobile when the agent asks. Agent-Up runs that vendor's no-browser or device-code flow on the Server, shows the sign-in link (and Codex device code) in the client, and then starts ACP. An unavailable agent shows why: a disabled capability offers Enable on the row, missing software names what to install, and missing settings point to Settings.

## Workspace-bound agent

Choosing an agent on Desktop binds that module to the workspace, then starts the session. On Mobile, the Agents + action opens a creation page of enabled modules; a successful start creates the bind, while a failed start stays on that page without leaving a stale bind. A workspace with no bound agent is normal, not an error.

## Unattended loop

Autonomy is experimental and off by default. Enablement persists on the Server; the environment variable still wins when it is set. The workspace **Settings** tab on Desktop and Mobile shows the ordered Server and workspace precondition checklist, Enable autonomy, Activate, Cancel, and the typed terminal for how the loop stopped. The Desktop Agent tab and Mobile Agents show a one-line summary that opens it. When a workspace has no permission policy, Mobile offers **Configure permissions** for worktree reads, worktree writes, commands, and network access; anything not allowed stays blocked. In the command center, the checklist and its actions are under **Settings › Chat**, with a tab for each workspace, and the chat shows only the conversation. A message typed while a turn is running is steering for the next turn; it does not cancel the loop.

<DocNext href="/docs/configuration" title="Configuration">
How to declare `dotnet[]` and `docker[]` shims.
</DocNext>
