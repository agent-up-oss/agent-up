using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Controllers;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Controllers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Controllers;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Controllers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Providers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Services;

namespace AgentUp.PackageSmoke.Shared.Factories;

public static class PackageSmokeServiceRegistry
{
    public static SmokeCommandController CreateSmokeCommandController()
    {
        var commands = new ProcessCommandRunner();
        var workDirectory = new SmokeWorkDirectoryProvider();
        var packageValidation = new PackageValidationController(platform =>
            PackageValidatorFactory.Create(platform, commands));
        var installerFlow = new InstallerFlowSmokeController(new InstallerFlowSmokeValidator());
        var installedService = new InstalledServiceSmokeController(platform =>
            InstalledServiceSmokeValidatorFactory.Create(platform, commands, new HttpServerProbe()));
        var validation = new SmokeValidationProvider(packageValidation, installerFlow, installedService, workDirectory);

        return new SmokeCommandController(
            new SmokeCommandParser(),
            workDirectory,
            new SmokeCommandService(validation));
    }
}
