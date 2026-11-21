# AgentUp.Sdk.Agent

The contract every built-in ACP agent capability implements. An agent capability names the ACP process the Server starts, the Nix packages it needs, the software that must be found before it can be enabled, any settings a user configures, and how subscription sign-in works for it.

Agent capabilities are compiled into the Server; enablement is Server-owned and agent capabilities add no `agent-up.json` sections.
