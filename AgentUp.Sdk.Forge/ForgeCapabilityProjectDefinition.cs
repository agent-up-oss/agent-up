using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Forge;

public abstract class ForgeCapabilityProjectDefinition
{
    public CapabilityProject CreateProject()
    {
        var builder = new ForgeCapabilityRegistrationBuilder();
        Register(builder);
        return new CapabilityProject(builder.Registrations);
    }

    protected abstract void Register(ForgeCapabilityRegistrationBuilder registry);
}
