using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation;

[TestFixture]
public class ProcessCommandRunnerTests
{
    [Test]
    public async Task RunAsync_reportsMissingCommandAsFailedResult()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("agent-up-command-that-does-not-exist", []));

        Assert.That(result.ExitCode, Is.EqualTo(127));
        Assert.That(result.Stderr, Is.Not.Empty);
    }
}
