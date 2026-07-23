using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;

namespace AgentUp.PackageSmoke.Tests.Features.InstallerFlowValidation.Provider;

[TestFixture]
public class InstallerFlowSmokeValidatorTests
{
    [Test]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-InstallerFlow", Guid.NewGuid().ToString());
        var previousFake = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable);

        try
        {
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, "1");

            var result = await new InstallerFlowSmokeValidator().ValidateAsync("ubuntu", workDir);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Join(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, previousFake);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
