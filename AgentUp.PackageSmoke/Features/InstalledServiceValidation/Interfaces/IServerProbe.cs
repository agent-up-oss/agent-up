using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;

public interface IServerProbe
{
    Task<string?> WaitForReadyAsync(
        string primaryUrl,
        string fallbackUrl,
        string outputFile,
        CancellationToken cancellationToken = default);
}
