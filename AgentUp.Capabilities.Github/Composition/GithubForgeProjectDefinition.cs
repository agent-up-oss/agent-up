using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Github.Composition;

public sealed class GithubForgeProjectDefinition : ForgeCapabilityProjectDefinition
{
    protected override void Register(ForgeCapabilityRegistrationBuilder registry)
        => registry.Add<Features.GithubCapability.Services.GithubForgeCapability>();
}
