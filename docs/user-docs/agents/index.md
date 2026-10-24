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

<DocContract label="Chrome">Desktop Agent · Mobile Agents</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab lists saved sessions and streams the selected workspace session. The live session is labeled Current; every other row is Resume. Once an agent is bound, the picker hides and the tab shows that agent. When a pasted-code sign-in is waiting, the tab collects the code and sends it to the Server. An unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal appear on the Agent tab; a prompt sent while it runs steers the next turn instead of stopping it.</DocSurface>
<DocSurface mobile>Agents lists saved sessions and Server-discovered ACP agents, then opens the selected chat as an inner page. The live session is labeled Current; every other row is Resume. A bound workspace hides the picker and shows that agent. The same unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal appear on Agents and in chat.</DocSurface>
</DocSurfaces>

<p><img src="/desktop-agents.png" alt="Desktop Agent tab showing the agent bound to Bob's workspace" /></p>
<p><img src="/mobile-agents.png" alt="Mobile Agents tab showing Codex" /></p>

## Workspace agent CLIs

Saved sessions from every enabled ACP agent appear together within the current workspace. Each row shows the agent, the agent-generated short description, and the branch last associated with the session. The live session is labeled Current. Select any other row to resume it. Sessions from another workspace never appear in this list.

Workspace agent chat uses ACP packages enabled on the Server. The picker lists first-party agents even when their package is off, plus any other enabled agent module. The Server wraps those launches in Nix. Enable the package from the agent row, Desktop capability-module chrome, or Mobile Settings. Clients never call the remote registry.

Sign in with the corresponding CLI's subscription login from Desktop or Mobile when the agent asks. Agent-Up runs that vendor's no-browser or device-code flow on the Server, shows the sign-in link (and Codex device code) in the client, and then starts ACP. An unavailable agent shows why: a disabled package offers Enable on the row, and a missing ACP executable names the command and how to install it.

## Workspace-bound agent

Choosing an agent on Desktop or Mobile binds that module to the workspace, then starts the session. The picker stays hidden while the bind is set. A workspace with no bound agent is normal interactive chat, not an error.

## Unattended loop

Autonomy is experimental and off by default. Enablement persists on the Server; the environment variable still wins when it is set. Desktop Agent and Mobile Agents show the ordered precondition checklist, Enable autonomy, Activate, Cancel, and the typed terminal for how the loop stopped. Mobile chat hides that checklist while subscription sign-in is in progress so the paste-code field stays on screen. A message typed while a turn is running is steering for the next turn; it does not cancel the loop.

<DocNext href="/docs/configuration" title="Configuration">
How to declare `dotnet[]` and `docker[]` shims.
</DocNext>
