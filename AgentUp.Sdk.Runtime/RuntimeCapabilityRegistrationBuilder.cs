using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Runtime;

public sealed class RuntimeCapabilityRegistrationBuilder
{
    private readonly List<ICapabilityRegistration> _registrations = [];

    public IReadOnlyList<ICapabilityRegistration> Registrations => _registrations;

    public RuntimeCapabilityRegistrationBuilder Add<TRuntime>()
        where TRuntime : class, IRuntimeCapability
    {
        _registrations.Add(new CapabilityRegistration<TRuntime>());
        return this;
    }
}
