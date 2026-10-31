---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace keeps its own saved ACP sessions and has one active session at a time. Sign in with that agent's subscription, never an API key.

Desktop chrome for the live session is the **Chat** tab. Mobile chrome for the picker is the **Agents** tab. Agent-Up does not collect API keys.
</DocWhat>

<DocSpine>
<DocBeat>Pick an available agent to bind it to the workspace</DocBeat>
<DocBeat>Complete subscription sign-in if the agent asks</DocBeat>
<DocBeat>Prompt in the live session, or watch an unattended loop</DocBeat>
</DocSpine>

<DocContract label="Chrome">Desktop Chat · Mobile Agents</DocContract>

<DocSurfaces>
<DocSurface desktop>The Chat tab opens the workspace's one live session. It does not list saved sessions or ask which one to resume. Once an agent is bound, the picker hides and the tab shows that agent. When a pasted-code sign-in is waiting, the tab collects the code and sends it to the Server. An unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal appear on the Chat tab; a prompt sent while it runs steers the next turn instead of stopping it.</DocSurface>
<DocSurface mobile>Agents shows the workspace's bound agent and opens its chat as an inner page. An unbound workspace shows a + action whose creation page lists agent capabilities already enabled on the Server. The same unattended loop checklist, Enable autonomy, Activate, Cancel, and typed terminal appear on Agents and in chat. Chat scrolls as one column, and the message box follows the transcript. The tab bar stays.</DocSurface>
</DocSurfaces>

<p><img src="/desktop-agents.png" alt="Desktop Chat tab showing the agent bound to Bob's workspace" /></p>
<p><img src="/mobile-agents.png" alt="Mobile Agents tab showing Codex" /></p>

## Workspace agent CLIs

A workspace has one live session. The Chat tab opens that session directly.

Workspace agent chat uses ACP packages enabled on the Server. The picker lists first-party agents even when their package is off, plus any other enabled agent module. The Server wraps those launches in Nix. Enable the package from the agent row, Desktop capability-module chrome, or Mobile Settings. Clients never call the remote registry.

Sign in with the corresponding CLI's subscription login from Desktop or Mobile when the agent asks. Agent-Up runs that vendor's no-browser or device-code flow on the Server, shows the sign-in link (and Codex device code) in the client, and then starts ACP. An unavailable agent shows why: a disabled package offers Enable on the row, and a missing ACP executable names the command and how to install it.

## Workspace-bound agent

Choosing an agent on Desktop binds that module to the workspace, then starts the session. On Mobile, the Agents + action opens a creation page of enabled modules; a successful start creates the bind, while a failed start stays on that page without leaving a stale bind. A workspace with no bound agent is normal, not an error.

## Unattended loop

Autonomy is experimental and off by default. Enablement persists on the Server; the environment variable still wins when it is set. Desktop Agent and Mobile Agents show the ordered Server and workspace precondition checklist, Enable autonomy, Activate, Cancel, and the typed terminal for how the loop stopped. When a workspace has no permission policy, Mobile offers **Configure permissions** for worktree reads, worktree writes, commands, and network access; anything not allowed stays blocked. Mobile chat hides the checklist while subscription sign-in is in progress so the sign-in action stays on screen. A message typed while a turn is running is steering for the next turn; it does not cancel the loop.

<DocNext href="/docs/configuration" title="Configuration">
How to declare `dotnet[]` and `docker[]` shims.
</DocNext>
