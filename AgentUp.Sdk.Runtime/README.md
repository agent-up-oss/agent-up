# AgentUp.Sdk.Runtime

The contract every built-in **runtime** capability implements. A runtime capability hosts applications from a named `agent-up.json` section (`dotnet`, `docker`, or a later runtime id), declares the commands it provides through its pinned Nix packages, and declares the software that must be found before it can be enabled.

Runtime capabilities are compiled into the Server.
