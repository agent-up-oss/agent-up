using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Controllers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Providers;
using AgentUp.PackageSmoke.Features.SmokeRuns.Services;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Factories;

public static class SmokeCommandControllerFactory
{
    public static SmokeCommandController Create()
    {
        var commands = new ProcessCommandRunner();
        var workDirectory = new SmokeWorkDirectoryProvider();
        var validation = new SmokeValidationProvider(commands, workDirectory);
        return new SmokeCommandController(
            new SmokeCommandParser(),
            workDirectory,
            new SmokeCommandService(validation));
    }
}
