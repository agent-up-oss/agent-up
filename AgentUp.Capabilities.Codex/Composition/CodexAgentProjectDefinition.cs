using AgentUp.Sdk.Agent;

namespace AgentUp.Capabilities.Codex.Composition;

public sealed class CodexAgentProjectDefinition : AgentCapabilityProjectDefinition
{
    protected override void Register(AgentCapabilityRegistrationBuilder registry)
        => registry.Add<Features.CodexCapability.Services.CodexAgentCapability>();
}
