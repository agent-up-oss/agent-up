using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDistribution.Unit;

[TestFixture]
public sealed class CapabilityManifestParserTests
{
    [Test]
    public void ParseCatalog_acceptsHttpsArtifactsWithSha256()
    {
        var parser = new CapabilityManifestParser();
        var catalog = parser.ParseCatalog("""
            {
              "schemaVersion": "1",
              "artifacts": [
                {
                  "capabilityId": "dotnet",
                  "version": "10.0.100",
                  "downloadUrl": "https://github.com/example/releases/dotnet.zip",
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
              ]
            }
            """);

        Assert.That(catalog.Artifacts.Single().CapabilityId, Is.EqualTo("dotnet"));
    }

    [Test]
    public void ParseCatalog_rejectsNonHttpsArtifacts()
    {
        var parser = new CapabilityManifestParser();

        var ex = Assert.Throws<InvalidOperationException>(() => parser.ParseCatalog("""
            {
              "schemaVersion": "1",
              "artifacts": [
                {
                  "capabilityId": "dotnet",
                  "version": "10.0.100",
                  "downloadUrl": "http://example.test/dotnet.zip",
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
              ]
            }
            """));

        Assert.That(ex!.Message, Does.Contain("HTTPS"));
    }
}
