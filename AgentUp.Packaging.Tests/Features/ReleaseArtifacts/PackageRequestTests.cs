using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class PackageRequestTests
{
    [TestCase("v1.2.3", "1.2.3")]
    [TestCase("1.2.3-ci.149", "1.2.3")]
    [TestCase("0.0.0-ci.149", "0.0.1")]
    public void WindowsInstallerVersion_usesValidMsiProductVersion(string version, string expected)
    {
        var request = new PackageRequest("/repo", "windows", "win-x64", version, "artifacts", "Release");

        Assert.That(request.WindowsInstallerVersion, Is.EqualTo(expected));
    }
}
