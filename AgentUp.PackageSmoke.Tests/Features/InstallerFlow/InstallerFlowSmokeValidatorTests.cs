using AgentUp.PackageSmoke.Features.InstallerFlow;

namespace AgentUp.PackageSmoke.Tests.Features.InstallerFlow;

[TestFixture]
public class InstallerFlowSmokeValidatorTests
{
    [Test]
    public async Task ValidateAsync_exercisesDryRunInstallerFlow()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "AgentUp-InstallerFlow", Guid.NewGuid().ToString());

        try
        {
            var result = await new InstallerFlowSmokeValidator().ValidateAsync("ubuntu", workDir);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Combine(workDir, "installer-flow.log")), Is.True);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
