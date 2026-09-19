---
title: Configuration
---

<DocEyebrow slice="Configuration" status="available" />

# Configuration

<DocWhat>
`agent-up.json` is the repository contract the Server reads at workspace start. It declares applications, services, capability requirements, ports, verification, and optional `commits.enabled`.

Applications do not reference Agent-Up packages. Capability inventory is how Server and Desktop discover ACP commands.
</DocWhat>

<DocMeta
  owner="Server configuration parsing plus AgentUp.Capabilities.*"
  tests="Server configuration suites and capability test projects"
  mcp="get_agent_up_json_format on /mcp/orchestration"
/>

<DocSpine>
<DocBeat>Read `get_agent_up_json_format` when the schema is unknown</DocBeat>
<DocBeat>Prefer capability sections over legacy executable strings</DocBeat>
<DocBeat>Reconcile declared versions with discovered inventory</DocBeat>
</DocSpine>

<DocContract label="Tool">get_agent_up_json_format</DocContract>

<DocFacts label="Orchestration resources">
<DocFact label="schema">agent-up://agent-up-json</DocFact>
<DocFact label="context">agent-up://context</DocFact>
</DocFacts>

## Capability inventory

Server and Desktop installments share inventory files, merged by capability id. Lookup order is `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then `/etc/agent-up/capabilities.json`, then a user overlay at `~/.config/agent-up/capabilities.local.json`, then `~/.config/agent-up/capabilities.json`, then `.agent-up-dev/capabilities.json` walking up from the Server's working directory. Earlier files win for a field; later files fill unspecified fields.

Server operators can still override a command or its arguments in `appsettings.json` under `Agents:Codex`, `Agents:Cursor`, or `Agents:Claude`. Services may have a different `PATH` from an interactive terminal, so use an absolute command path when the installed service cannot discover an adapter.

<DocNext href="/docs/configuration/reference" title="User reference">
The JSON field contract.
</DocNext>
