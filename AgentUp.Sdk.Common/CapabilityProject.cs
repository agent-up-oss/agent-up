namespace AgentUp.Sdk.Common;

public sealed class CapabilityProject
{
    public CapabilityProject(IReadOnlyList<ICapabilityRegistration> registrations)
    {
        Registrations = registrations;
    }

    public IReadOnlyList<ICapabilityRegistration> Registrations { get; }
}
