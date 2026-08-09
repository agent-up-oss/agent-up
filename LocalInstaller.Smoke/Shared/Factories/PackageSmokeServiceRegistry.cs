using LocalInstaller.Smoke.Features.InstalledServiceValidation.Controllers;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Factories;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Providers;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.Controllers;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.Services;
using LocalInstaller.Smoke.Features.PackageValidation.Controllers;
using LocalInstaller.Smoke.Features.PackageValidation.Factories;
using LocalInstaller.Smoke.Features.PackageValidation.Providers;
using LocalInstaller.Smoke.Features.RuntimeSecurity.Providers;
using LocalInstaller.Smoke.Features.RuntimeSecurity.Services;
using LocalInstaller.Smoke.Features.SmokeRuns.Controllers;
using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.Providers;
using LocalInstaller.Smoke.Features.SmokeRuns.Services;

namespace LocalInstaller.Smoke.Shared.Factories;

public static class PackageSmokeServiceRegistry
{
    public static SmokeCommandController CreateSmokeCommandController(SmokeProductManifest? product = null)
    {
        var commands = new ProcessCommandRunner();
        var workDirectory = new SmokeWorkDirectoryProvider();
        var packageValidation = new PackageValidationController(platform =>
            PackageValidatorFactory.Create(platform, commands));
        var installerFlow = new InstallerFlowSmokeController(new InstallerFlowSmokeValidator());
        var installedService = new InstalledServiceSmokeController(platform =>
            InstalledServiceSmokeValidatorFactory.Create(platform, commands, new HttpServerProbe(),
                new RuntimeSecurityChecks(new SystemNetworkStateProvider(), new HttpClient())));
        var validation = new SmokeValidationProvider(packageValidation, installerFlow, installedService, workDirectory);

        return new SmokeCommandController(
            new SmokeCommandService(validation, workDirectory, new SmokeCommandParser(product)));
    }
}
