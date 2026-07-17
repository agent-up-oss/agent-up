using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.RuntimeSecurity;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity;

internal sealed class NullRuntimeSecurityChecks : IRuntimeSecurityChecks
{
    public Task RunAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
