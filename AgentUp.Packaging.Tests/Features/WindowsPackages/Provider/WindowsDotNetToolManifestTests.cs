using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
namespace AgentUp.Packaging.Tests.Features.WindowsPackages.Provider;

[TestFixture]
public class WindowsDotNetToolManifestTests
{
    [Test]
    public void Manifest_pinsWixDotNetTool()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var manifest = File.ReadAllText(Path.Join(root, "packaging", "windows", "dotnet-tools.json"));

        Assert.That(manifest, Does.Contain("\"wix\""));
        Assert.That(manifest, Does.Contain("\"version\": \"7.0.0\""));
        Assert.That(manifest, Does.Contain("\"commands\""));
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {startDirectory}.");
    }
}
