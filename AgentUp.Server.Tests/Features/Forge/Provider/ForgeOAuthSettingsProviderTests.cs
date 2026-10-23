using AgentUp.Server.Features.Forge.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeOAuthSettingsProviderTests
{
    [Test]
    public void Read_usesHostNeutralForgeVariables()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AGENTUP_FORGE_CLIENT_ID"] = "forge-client",
                ["AGENTUP_FORGE_CLIENT_SECRET"] = "forge-secret",
                ["AGENTUP_FORGE_REDIRECT_URI"] = "http://localhost/api/forge/callback"
            })
            .Build();

        var oauth = new ForgeOAuthSettingsProvider(configuration).Read();

        Assert.That(oauth.IsConfigured, Is.True);
        Assert.That(oauth.ClientId, Is.EqualTo("forge-client"));
        Assert.That(oauth.RedirectUri, Does.Contain("/api/forge/callback"));
    }

    [Test]
    public void Read_ignoresHostNamedClientVariables()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AGENTUP_GITHUB_CLIENT_ID"] = "github-client",
                ["AGENTUP_GITHUB_CLIENT_SECRET"] = "github-secret",
                ["AGENTUP_GITHUB_REDIRECT_URI"] = "http://localhost/callback"
            })
            .Build();

        var oauth = new ForgeOAuthSettingsProvider(configuration).Read();

        Assert.That(oauth.IsConfigured, Is.False);
        Assert.That(oauth.ClientId, Is.Empty);
    }
}
