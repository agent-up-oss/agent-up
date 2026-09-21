using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Server.Tests.Support;

// The only project definitions in this assembly, so a test can hand the assembly itself to
// CapabilityModuleLoadProvider as a packed module and know which capability comes back.
internal sealed class StubRuntimeProjectDefinition : RuntimeCapabilityProjectDefinition
{
    protected override void Register(RuntimeCapabilityRegistrationBuilder registry)
        => registry.Add<StubRuntimeCapability>();
}

internal sealed class StubAgentProjectDefinition : AgentCapabilityProjectDefinition
{
    protected override void Register(AgentCapabilityRegistrationBuilder registry)
        => registry.Add<StubAgentCapability>();
}
