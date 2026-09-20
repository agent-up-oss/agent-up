# AgentUp.Sdk.Agent

MSBuild SDK for Agent-Up **agent** capability modules. An agent module integrates an ACP agent. Enablement is Server-owned; agent modules do not add `agent-up.json` sections.

## In-repo consumption

```xml
<Project>
  <Import Project="..\AgentUp.Sdk.Agent\Sdk\Sdk.props" />
  <!-- project body -->
  <Import Project="..\AgentUp.Sdk.Agent\Sdk\Sdk.targets" />
</Project>
```

Packed NuGet consumers use `<Project Sdk="AgentUp.Sdk.Agent">`.

Implement `AgentCapabilityProjectDefinition` and register an `IAgentCapability`.
