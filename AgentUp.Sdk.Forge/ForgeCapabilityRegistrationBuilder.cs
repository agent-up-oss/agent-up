using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Forge;

public sealed class ForgeCapabilityRegistrationBuilder
{
    private readonly List<ICapabilityRegistration> _registrations = [];

    public IReadOnlyList<ICapabilityRegistration> Registrations => _registrations;

    public ForgeCapabilityRegistrationBuilder Add<TForge>()
        where TForge : class, IForgeCapability
    {
        _registrations.Add(new CapabilityRegistration<TForge>());
        return this;
    }
}
