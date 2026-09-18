---
title: Configuration
---

<DocEyebrow slice="Configuration" status="available" />

# Configuration

<DocFocus>
`agent-up.json` is the repository contract. Capability inventory is how Server and Desktop discover ACP commands.
</DocFocus>

**Owner:** Server configuration parsing plus `AgentUp.Capabilities.*` adapters. Tests live in Server configuration suites and capability test projects. MCP: `get_agent_up_json_format` on `/mcp/orchestration`. JSON contract: [User reference](/docs/configuration/reference).

## What it is

The file declares applications, services, capability requirements, ports, verification, and optional `commits.enabled`. Applications do not reference Agent-Up packages. Inventory files declare `command`, `arguments`, and `versionArguments` for Codex, Cursor, and Claude.

<DocSpine>
<DocBeat selected>Read `get_agent_up_json_format` when the schema is unknown</DocBeat>
<DocBeat>Prefer capability sections over legacy executable strings</DocBeat>
<DocBeat>Reconcile declared versions with discovered inventory</DocBeat>
</DocSpine>

<DocContract>get_agent_up_json_format</DocContract>

Resources `agent-up://agent-up-json` and `agent-up://context` live on `/mcp/orchestration`.

## Capability inventory

Server and Desktop installments share inventory files, merged by capability id. Lookup order is `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then `/etc/agent-up/capabilities.json`, then a user overlay at `~/.config/agent-up/capabilities.local.json`, then `~/.config/agent-up/capabilities.json`, then `.agent-up-dev/capabilities.json` walking up from the Server's working directory. Earlier files win for a field; later files fill unspecified fields.

Server operators can still override a command or its arguments in `appsettings.json` under `Agents:Codex`, `Agents:Cursor`, or `Agents:Claude`. Services may have a different `PATH` from an interactive terminal, so use an absolute command path when the installed service cannot discover an adapter.
