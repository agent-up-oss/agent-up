namespace AgentUp.Sdk.Common;

public sealed class CapabilityRegistration<TImplementation> : ICapabilityRegistration
    where TImplementation : class
{
    public Type ImplementationType { get; } = typeof(TImplementation);
}
