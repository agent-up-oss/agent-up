namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

public interface ICapabilitySearchPathProvider
{
    IReadOnlyList<string> Directories();
}
