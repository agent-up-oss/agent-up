using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Docker.Composition;

public sealed class DockerRuntimeProjectDefinition : RuntimeCapabilityProjectDefinition
{
    protected override void Register(RuntimeCapabilityRegistrationBuilder registry)
        => registry.Add<Features.DockerCapability.Services.DockerRuntimeCapability>();
}
