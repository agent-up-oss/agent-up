using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Models;

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

    [Test]
    public void WindowsPackageLayout_usesProductSlugForArtifactNamesAndKeepsAgentUpDefault()
    {
        var agentUp = new PackageRequest("/repo", "windows", "win-x64", "1.2.3", "artifacts", "Release");
        var orbit = new PackageRequest(
            "/repo",
            "windows",
            "win-x64",
            "1.2.3",
            "artifacts",
            "Release",
            productManifest: new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK"));

        Assert.That(WindowsPackageLayout.From(agentUp).ProductMsiOutputPath, Is.EqualTo(Path.GetFullPath("/repo/artifacts/agent-up-windows-win-x64.msi")));
        Assert.That(WindowsPackageLayout.From(orbit).ProductMsiOutputPath, Is.EqualTo(Path.GetFullPath("/repo/artifacts/orbit-desk-windows-win-x64.msi")));
    }

    [Test]
    public void Constructor_rejectsProductSlugTraversal()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new PackageRequest(
                "/repo",
                "windows",
                "win-x64",
                "1.2.3",
                "artifacts",
                "Release",
                productManifest: new PackageProductManifest("Outside", "../outside", "OUTSIDE")));

        Assert.That(exception!.ParamName, Is.EqualTo("Slug"));
    }

    [TestCase("ProductName", "Bad/Product")]
    [TestCase("WindowsUpgradeCode", "not-a-guid")]
    [TestCase("WindowsServiceName", "bad\\service")]
    [TestCase("WindowsCliShimName", "CON.cmd")]
    [TestCase("WindowsCliShimName", "COM¹.cmd")]
    [TestCase("WindowsCliShimName", "LPT².cmd")]
    [TestCase("WindowsCliShimName", "orbit?.cmd")]
    [TestCase("WindowsCliShimName", "orbit.cmd.")]
    [TestCase("WindowsServerUrl", "not-a-url")]
    public void Constructor_rejectsInvalidProductManifestValues(string expectedParamName, string invalidValue)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new PackageRequest(
                "/repo",
                "windows",
                "win-x64",
                "1.2.3",
                "artifacts",
                "Release",
                productManifest: ProductWithInvalidValue(expectedParamName, invalidValue)));

        Assert.That(exception!.ParamName, Is.EqualTo(expectedParamName));
    }

    [TestCase("ProductName")]
    [TestCase("Slug")]
    [TestCase("EnvironmentPrefix")]
    public void ProductManifest_IsValidRejectsRequiredFieldsOverwrittenAfterConstruction(string field)
    {
        var manifest = new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK");

        manifest = field switch
        {
            "ProductName" => manifest with { ProductName = "Bad/Product" },
            "Slug" => manifest with { Slug = "../outside" },
            "EnvironmentPrefix" => manifest with { EnvironmentPrefix = "orbit-desk" },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        Assert.That(manifest.IsValid(), Is.False);
    }

    private static PackageProductManifest ProductWithInvalidValue(string field, string value)
        => field switch
        {
            "ProductName" => new PackageProductManifest(value, "orbit-desk", "ORBITDESK"),
            "WindowsUpgradeCode" => new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
            {
                WindowsUpgradeCode = value
            },
            "WindowsServiceName" => new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
            {
                WindowsServiceName = value
            },
            "WindowsCliShimName" => new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
            {
                WindowsCliShimName = value
            },
            "WindowsServerUrl" => new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
            {
                WindowsServerUrl = value
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
}
