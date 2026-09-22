# AgentUp.Sdk.Runtime

MSBuild SDK for Agent-Up **runtime** capability modules. A runtime module hosts applications from a named `agent-up.json` section (`dotnet`, `docker`, or a later runtime id).

## In-repo consumption

First-party modules import the SDK props and targets from this project:

```xml
<Project>
  <Import Project="..\AgentUp.Sdk.Runtime\Sdk\Sdk.props" />
  <!-- project body -->
  <Import Project="..\AgentUp.Sdk.Runtime\Sdk\Sdk.targets" />
</Project>
```

Packed NuGet consumers use `<Project Sdk="AgentUp.Sdk.Runtime">`.

Implement `RuntimeCapabilityProjectDefinition` and register an `IRuntimeCapability`.
