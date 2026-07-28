using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class OfficialCapabilityCatalogProviderTests
{
    [Test]
    public async Task AgentUpCatalogProvider_returnsBaselineModuleList()
    {
        var previousCatalog = Environment.GetEnvironmentVariable(OfficialCapabilityCatalogProvider.CatalogUrlVariable);
        Environment.SetEnvironmentVariable(OfficialCapabilityCatalogProvider.CatalogUrlVariable, null);

        try
        {
            ICapabilityCatalogProvider provider = new OfficialCapabilityCatalogProvider();
            var entries = await provider.GetCatalogAsync();

            Assert.That(entries.Select(entry => new
            {
                entry.Id,
                entry.DisplayName,
                entry.Description,
                Version = entry.Versions.Single().Version,
                DownloadUrl = entry.Versions.Single().DownloadUrl.AbsoluteUri,
                entry.Versions.Single().Sha256
            }), Is.EqualTo(new[]
            {
                new
                {
                    Id = "dotnet",
                    DisplayName = ".NET",
                    Description = "Discovers and manages .NET SDK versions for Agent-Up workspaces.",
                    Version = "10.0.x",
                    DownloadUrl = "https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-capability-dotnet.zip",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000"
                },
                new
                {
                    Id = "docker",
                    DisplayName = "Docker",
                    Description = "Discovers Docker and manages Docker-backed workspace services.",
                    Version = "27.x",
                    DownloadUrl = "https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-capability-docker.zip",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000"
                }
            }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(OfficialCapabilityCatalogProvider.CatalogUrlVariable, previousCatalog);
        }
    }
}
