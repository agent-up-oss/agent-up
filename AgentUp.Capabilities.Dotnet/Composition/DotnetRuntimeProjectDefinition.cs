using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Dotnet.Composition;

public sealed class DotnetRuntimeProjectDefinition : RuntimeCapabilityProjectDefinition
{
    protected override void Register(RuntimeCapabilityRegistrationBuilder registry)
        => registry.Add<Features.DotnetCapability.Services.DotnetRuntimeCapability>();
}
