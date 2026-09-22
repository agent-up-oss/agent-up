using AgentUp.Sdk.Agent;

namespace AgentUp.Capabilities.Cursor.Composition;

public sealed class CursorAgentProjectDefinition : AgentCapabilityProjectDefinition
{
    protected override void Register(AgentCapabilityRegistrationBuilder registry)
        => registry.Add<Features.CursorCapability.Services.CursorAgentCapability>();
}
