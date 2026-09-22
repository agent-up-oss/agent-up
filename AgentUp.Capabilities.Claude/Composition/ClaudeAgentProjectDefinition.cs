using AgentUp.Sdk.Agent;

namespace AgentUp.Capabilities.Claude.Composition;

public sealed class ClaudeAgentProjectDefinition : AgentCapabilityProjectDefinition
{
    protected override void Register(AgentCapabilityRegistrationBuilder registry)
        => registry.Add<Features.ClaudeCapability.Services.ClaudeAgentCapability>();
}
