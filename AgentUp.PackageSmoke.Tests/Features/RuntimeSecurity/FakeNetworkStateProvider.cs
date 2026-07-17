using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Net;
using AgentUp.PackageSmoke.Features.RuntimeSecurity;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity;

internal sealed class FakeNetworkStateProvider : INetworkStateProvider
{
    private readonly IPEndPoint[] _listeners;

    public FakeNetworkStateProvider(params IPEndPoint[] listeners)
    {
        _listeners = listeners;
    }

    public IPEndPoint[] GetActiveTcpListeners() => _listeners;
}
