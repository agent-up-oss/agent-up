using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public sealed class CapabilityWorkspaceProviderTests
{
    [Test]
    public async Task Prepare_generatesBuildableDotnetSmokeProject()
    {
        var workDir = Path.Join(Path.GetTempPath(), "AgentUp-CapabilityWorkspaceProvider", Guid.NewGuid().ToString());

        try
        {
            var repo = new CapabilityWorkspaceProvider().Prepare(workDir);
            var result = await new DotnetSmokeBuildProvider(new ProcessCommandRunner()).BuildAsync(repo);

            Assert.That(result, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
