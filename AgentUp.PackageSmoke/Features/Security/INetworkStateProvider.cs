using System.Net;

namespace AgentUp.PackageSmoke.Features.Security;

public interface INetworkStateProvider
{
    IPEndPoint[] GetActiveTcpListeners();
}
