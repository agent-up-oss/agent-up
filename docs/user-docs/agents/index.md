---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocFocus>
Each workspace has one ACP agent session. Sign in with that agent's subscription, never an API key.
</DocFocus>

## What it is

Desktop chrome for the live session is the **Agent** tab. Mobile chrome for the picker is the **Agents** tab. The Server discovers Codex, Cursor, and Claude adapters from capability inventory and runs their no-browser or device-code login. Agent-Up does not collect API keys.

<DocSpine>
<DocBeat selected>Pick an available agent</DocBeat>
<DocBeat>Complete subscription sign-in if the agent asks</DocBeat>
<DocBeat>Prompt in the live session</DocBeat>
</DocSpine>

<DocContract>Agent tab · Agents tab</DocContract>

<DocSurface mobile>Agents lists Server-discovered ACP agents and opens chat as an inner page.</DocSurface>

<DocSurface desktop>The Agent tab streams the live workspace session.</DocSurface>

Next in this slice: [Configuration](/docs/configuration) for how inventory declares ACP commands.

## Workspace agent CLIs

Workspace agent chat uses first-party Codex, Cursor, and Claude capability adapters on the Server. Those adapters launch only the ACP command declared in Agent-Up capability inventory, not a hardcoded executable name.

Sign in with the corresponding CLI's subscription login from Desktop or Mobile when the agent asks. Agent-Up runs that vendor's no-browser or device-code flow on the Server, shows the sign-in link (and Codex device code) in the client, and then starts ACP. An unavailable executable is disabled in the Desktop and Mobile agent picker.
