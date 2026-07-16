using System.Net;
using AgentUp.PackageSmoke.Features.Security;

namespace AgentUp.PackageSmoke.Tests.Features.Security;

internal sealed class FakeNetworkStateProvider : INetworkStateProvider
{
    private readonly IPEndPoint[] _listeners;

    public FakeNetworkStateProvider(params IPEndPoint[] listeners)
    {
        _listeners = listeners;
    }

    public IPEndPoint[] GetActiveTcpListeners() => _listeners;
}
