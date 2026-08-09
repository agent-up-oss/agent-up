using System.Net;
using System.Net.NetworkInformation;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;

public sealed class SystemNetworkStateProvider : INetworkStateProvider
{
    public IPEndPoint[] GetActiveTcpListeners()
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
}
