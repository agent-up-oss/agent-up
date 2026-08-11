using System.Net;

namespace LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;

public interface INetworkStateProvider
{
    IPEndPoint[] GetActiveTcpListeners();
}
