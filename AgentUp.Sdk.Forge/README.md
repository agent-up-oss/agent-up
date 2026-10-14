# AgentUp.Sdk.Forge

MSBuild SDK for Agent-Up **forge** capability modules. A forge module talks to a
repository host. It has no Nix closure to deliver and no ACP session to run.
Enablement is Server-owned; forge modules do not add `agent-up.json` sections.

## In-repo consumption

```xml
<Project>
  <Import Project="..\AgentUp.Sdk.Forge\Sdk\Sdk.props" />
  <!-- project body -->
  <Import Project="..\AgentUp.Sdk.Forge\Sdk\Sdk.targets" />
</Project>
```

Packed NuGet consumers use `<Project Sdk="AgentUp.Sdk.Forge">`.

Implement `ForgeCapabilityProjectDefinition` and register an `IForgeCapability`.
