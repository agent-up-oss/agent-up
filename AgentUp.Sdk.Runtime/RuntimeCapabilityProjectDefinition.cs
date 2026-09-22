using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Runtime;

public abstract class RuntimeCapabilityProjectDefinition
{
    public CapabilityProject CreateProject()
    {
        var builder = new RuntimeCapabilityRegistrationBuilder();
        Register(builder);
        return new CapabilityProject(builder.Registrations);
    }

    protected abstract void Register(RuntimeCapabilityRegistrationBuilder registry);
}
