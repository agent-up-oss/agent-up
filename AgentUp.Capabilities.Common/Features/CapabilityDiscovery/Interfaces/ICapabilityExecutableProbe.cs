namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

public interface ICapabilityExecutableProbe
{
    bool IsExecutable(string path);
}
