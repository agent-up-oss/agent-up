using System.Net;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

public interface INetworkStateProvider
{
    IPEndPoint[] GetActiveTcpListeners();
}
