using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.Services;

namespace LocalInstaller.Smoke.Tests.Features.InstallerFlowValidation.Provider;

[TestFixture]
public class InstallerFlowSmokeValidatorTests
{
    private static ProductManifest AcmeStudio => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };
    private static string AgentUpFakeInstallerVariable => new ProductManifest("Agent-Up", "agent-up", "AGENTUP").FakeInstallerVariable;

    [Test]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-InstallerFlow", Guid.NewGuid().ToString());
        var previousFake = Environment.GetEnvironmentVariable(AgentUpFakeInstallerVariable);

        try
        {
            Environment.SetEnvironmentVariable(AgentUpFakeInstallerVariable, "1");

            var result = await new InstallerFlowSmokeValidator().ValidateAsync("ubuntu", workDir);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Join(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentUpFakeInstallerVariable, previousFake);
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
