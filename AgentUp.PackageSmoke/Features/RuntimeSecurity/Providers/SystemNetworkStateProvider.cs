using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Net;
using System.Net.NetworkInformation;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;

public sealed class SystemNetworkStateProvider : INetworkStateProvider
{
    public IPEndPoint[] GetActiveTcpListeners()
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
}
