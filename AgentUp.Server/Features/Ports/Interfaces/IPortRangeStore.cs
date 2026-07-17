using AgentUp.Server.Features.Ports.Models;

namespace AgentUp.Server.Features.Ports.Interfaces;

public interface IPortRangeStore
{
    PortRangeData? Load();
    Task SaveAsync(PortRangeData data);
}
