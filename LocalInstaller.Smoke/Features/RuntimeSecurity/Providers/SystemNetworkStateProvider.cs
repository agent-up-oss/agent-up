using System.Net;
using System.Net.NetworkInformation;
using LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;

namespace LocalInstaller.Smoke.Features.RuntimeSecurity.Providers;

public sealed class SystemNetworkStateProvider : INetworkStateProvider
{
    public IPEndPoint[] GetActiveTcpListeners()
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
}
