using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Composition;

public sealed class ForgejoForgeProjectDefinition : ForgeCapabilityProjectDefinition
{
    protected override void Register(ForgeCapabilityRegistrationBuilder registry)
        => registry.Add<Features.ForgejoCapability.Services.ForgejoForgeCapability>();
}
