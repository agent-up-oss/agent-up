namespace AgentUp.Server.Features.Ports.Interfaces;

public interface IPortAvailabilityProvider
{
    bool ArePortsAvailable(int basePort, int count);
}
