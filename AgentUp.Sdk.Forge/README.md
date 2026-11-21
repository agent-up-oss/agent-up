# AgentUp.Sdk.Forge

The contract every built-in **forge** capability implements. A forge capability talks to a
repository host. It has no Nix closure to deliver and no ACP session to run, so it needs no
software on the Server host. Forge capabilities are compiled into the Server; enablement is
Server-owned and forge capabilities add no `agent-up.json` sections.
