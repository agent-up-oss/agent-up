using System.Net;
using LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;

namespace LocalInstaller.Smoke.Tests.Features.RuntimeSecurity.Fake;

internal sealed class FakeNetworkStateProvider : INetworkStateProvider
{
    private readonly IPEndPoint[] _listeners;

    public FakeNetworkStateProvider(params IPEndPoint[] listeners)
    {
        _listeners = listeners;
    }

    public IPEndPoint[] GetActiveTcpListeners() => _listeners;
}
