---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace has one ACP agent session. Sign in with that agent's subscription, never an API key.

Desktop chrome for the live session is the **Agent** tab. Mobile chrome for the picker is the **Agents** tab. Agent-Up does not collect API keys.
</DocWhat>

<DocSpine>
<DocBeat>Pick an available agent</DocBeat>
<DocBeat>Complete subscription sign-in if the agent asks</DocBeat>
<DocBeat>Prompt in the live session</DocBeat>
</DocSpine>

<DocContract label="Chrome">Desktop Agent · Mobile Agents</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab streams the live workspace session.</DocSurface>
<DocSurface mobile>Agents lists Server-discovered ACP agents and opens chat as an inner page.</DocSurface>
</DocSurfaces>

## Workspace agent CLIs

Workspace agent chat uses ACP packages enabled on the Server. The picker lists those modules by id. The Server wraps those launches in Nix. Enable the package from Desktop capability-module chrome or Mobile Settings. Clients never call the remote registry.

Sign in with the corresponding CLI's subscription login from Desktop or Mobile when the agent asks. Agent-Up runs that vendor's no-browser or device-code flow on the Server, shows the sign-in link (and Codex device code) in the client, and then starts ACP. An unavailable executable is disabled in the Desktop and Mobile agent picker.

<DocNext href="/docs/configuration" title="Configuration">
How to declare `dotnet[]` and `docker[]` shims.
</DocNext>
