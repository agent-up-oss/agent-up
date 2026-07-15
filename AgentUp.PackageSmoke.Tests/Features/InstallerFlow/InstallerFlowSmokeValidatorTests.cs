using AgentUp.PackageSmoke.Features.InstallerFlow;
using AgentUp.Installers.Features.Execution;

namespace AgentUp.PackageSmoke.Tests.Features.InstallerFlow;

[TestFixture]
public class InstallerFlowSmokeValidatorTests
{
    [Test]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "AgentUp-InstallerFlow", Guid.NewGuid().ToString());
        var previousFake = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable);

        try
        {
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, "1");

            var result = await new InstallerFlowSmokeValidator().ValidateAsync("ubuntu", workDir);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Combine(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, previousFake);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
