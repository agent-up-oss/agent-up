using AgentUp.InstallerConfig;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;

namespace AgentUp.PackageSmoke.Tests.Features.InstallerFlowValidation.Provider;

[TestFixture]
public class InstallerFlowSmokeValidatorTests
{
    private static ProductManifest AcmeStudio => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    [Test]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-InstallerFlow", Guid.NewGuid().ToString());
        var previousFake = Environment.GetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable);

        try
        {
            Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, "1");

            var result = await new InstallerFlowSmokeValidator().ValidateAsync("ubuntu", workDir);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Join(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, previousFake);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    [TestCase("ubuntu")]
    [TestCase("macos")]
    [TestCase("windows")]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow_forAcmeStudio(string platform)
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-InstallerFlow-AcmeStudio", Guid.NewGuid().ToString());
        var fakeVar = AcmeStudio.FakeInstallerVariable;
        var previousFake = Environment.GetEnvironmentVariable(fakeVar);

        try
        {
            Environment.SetEnvironmentVariable(fakeVar, "1");

            var result = await new InstallerFlowSmokeValidator().ValidateAsync(platform, workDir, AcmeStudio);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Join(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(fakeVar, previousFake);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
