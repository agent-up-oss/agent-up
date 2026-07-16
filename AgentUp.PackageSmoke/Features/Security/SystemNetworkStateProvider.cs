using System.Net;
using System.Net.NetworkInformation;

namespace AgentUp.PackageSmoke.Features.Security;

public sealed class SystemNetworkStateProvider : INetworkStateProvider
{
    public IPEndPoint[] GetActiveTcpListeners()
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
}
