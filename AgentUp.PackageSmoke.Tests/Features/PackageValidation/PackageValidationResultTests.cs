using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation;

[TestFixture]
public class PackageValidationResultTests
{
    [Test]
    public void ToEnvironmentFile_quotesPathsForShell()
    {
        var result = new PackageValidationResult("/tmp/server path/AgentUp.Server", "/tmp/cli'path/AgentUp.CLI", []);

        var env = result.ToEnvironmentFile();

        Assert.That(env, Does.Contain("SERVER_PATH='/tmp/server path/AgentUp.Server'"));
        Assert.That(env, Does.Contain("CLI_PATH='/tmp/cli'\"'\"'path/AgentUp.CLI'"));
    }
}
