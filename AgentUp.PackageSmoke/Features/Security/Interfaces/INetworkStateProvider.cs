using System.Net;

namespace AgentUp.PackageSmoke.Features.Security.Providers;

public interface INetworkStateProvider
{
    IPEndPoint[] GetActiveTcpListeners();
}
