using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

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

    [Test]
    public void Constructor_rejectsOutputDirectoryTraversal()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new PackageRequest("/repo", "windows", "win-x64", "1.2.3", "../outside", "Release"));

        Assert.That(exception!.ParamName, Is.EqualTo("OutputDirectory"));
    }

    [Test]
    public void Constructor_rejectsPlatformPathComponents()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new PackageRequest("/repo", "../windows", "win-x64", "1.2.3", "artifacts", "Release"));

        Assert.That(exception!.ParamName, Is.EqualTo("Platform"));
    }

    [Test]
    public void Constructor_normalizesRelativePayloadRootUnderRepository()
    {
        var request = new PackageRequest("/repo", "windows", "win-x64", "1.2.3", "artifacts", "Release", Path.Join("payloads", "win-x64"));

        Assert.That(request.PayloadRoot, Is.EqualTo(Path.GetFullPath(Path.Join("/repo", "payloads", "win-x64"))));
        Assert.That(request.DesktopPayloadDirectory, Is.EqualTo(Path.GetFullPath(Path.Join("/repo", "payloads", "win-x64", "desktop"))));
    }
}
