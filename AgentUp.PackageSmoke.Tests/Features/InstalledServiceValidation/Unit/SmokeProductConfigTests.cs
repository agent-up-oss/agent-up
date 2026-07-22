using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Unit;

[TestFixture]
public sealed class SmokeProductConfigTests
{
    [Test]
    public void Product_returnsAgentUpDefaultWhenProductConfigIsOmitted()
    {
        var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", "/artifacts", "/work");

        Assert.That(request.Product, Is.EqualTo(SmokeProductConfig.AgentUp));
    }

    [TestCase("ServiceName", "acme;rm")]
    [TestCase("ServiceName", "-acme-server")]
    [TestCase("CliShimName", "acme/server")]
    [TestCase("CliShimName", "Acme")]
    [TestCase("ArtifactBaseName", "-acme")]
    [TestCase("ArtifactBaseName", "acme package")]
    [TestCase("InstallDirName", "../Acme")]
    [TestCase("InstallDirName", "Acme;Remove")]
    [TestCase("WorkspaceConfigFileName", "../acme.json")]
    [TestCase("WorkspaceConfigFileName", "-acme.json")]
    [TestCase("WorkspaceConfigFileName", "acme.json;git")]
    [TestCase("DisplayName", "Acme\nSetup")]
    public void Constructor_rejectsUnsafeProductMetadata(string propertyName, string unsafeValue)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateProduct(propertyName, unsafeValue));

        Assert.That(exception!.ParamName, Is.EqualTo(propertyName));
    }

    [Test]
    public void Constructor_acceptsSafeProductMetadata()
    {
        var product = new SmokeProductConfig(
            ServiceName: "acme-server",
            CliShimName: "acme",
            ArtifactBaseName: "acme",
            DisplayName: "Acme Setup",
            InstallDirName: "Acme_Setup.1",
            WorkspaceConfigFileName: "acme_setup.json");

        Assert.That(product.ServiceName, Is.EqualTo("acme-server"));
    }

    private static SmokeProductConfig CreateProduct(string propertyName, string value)
        => new(
            ServiceName: propertyName == "ServiceName" ? value : "acme-server",
            CliShimName: propertyName == "CliShimName" ? value : "acme",
            ArtifactBaseName: propertyName == "ArtifactBaseName" ? value : "acme",
            DisplayName: propertyName == "DisplayName" ? value : "Acme",
            InstallDirName: propertyName == "InstallDirName" ? value : "Acme",
            WorkspaceConfigFileName: propertyName == "WorkspaceConfigFileName" ? value : "acme.json");
}
