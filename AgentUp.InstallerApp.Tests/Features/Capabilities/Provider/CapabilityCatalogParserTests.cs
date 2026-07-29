using AgentUp.InstallerApp.Features.Capabilities.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityCatalogParserTests
{
    [Test]
    public void ParseCatalog_rejectsNullDownloadUrlWithValidationError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new CapabilityCatalogParser().ParseCatalog("""
            {
              "schemaVersion": "1.0",
              "artifacts": [
                {
                  "capabilityId": "dotnet",
                  "version": "10.0.x",
                  "downloadUrl": null,
                  "sha256": "0000000000000000000000000000000000000000000000000000000000000000"
                }
              ]
            }
            """));

        Assert.That(ex!.Message, Is.EqualTo("Capability artifact 'dotnet' downloadUrl is required."));
    }

    [Test]
    public void ParseCatalog_rejectsNullSha256WithValidationError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new CapabilityCatalogParser().ParseCatalog("""
            {
              "schemaVersion": "1.0",
              "artifacts": [
                {
                  "capabilityId": "docker",
                  "version": "27.x",
                  "downloadUrl": "https://example.invalid/docker.zip",
                  "sha256": null
                }
              ]
            }
            """));

        Assert.That(ex!.Message, Is.EqualTo("Capability artifact 'docker' SHA-256 value is required."));
    }
}
