using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent;

public abstract class AgentCapabilityProjectDefinition
{
    public CapabilityProject CreateProject()
    {
        var builder = new AgentCapabilityRegistrationBuilder();
        Register(builder);
        return new CapabilityProject(builder.Registrations);
    }

    protected abstract void Register(AgentCapabilityRegistrationBuilder registry);
}
