using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent;

public sealed class AgentCapabilityRegistrationBuilder
{
    private readonly List<ICapabilityRegistration> _registrations = [];

    public IReadOnlyList<ICapabilityRegistration> Registrations => _registrations;

    public AgentCapabilityRegistrationBuilder Add<TAgent>()
        where TAgent : class, IAgentCapability
    {
        _registrations.Add(new CapabilityRegistration<TAgent>());
        return this;
    }
}
