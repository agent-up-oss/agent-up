using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpRemoteAccessProviderTests
{
    [Test]
    public void RemoteAccessIsOffWhenNothingIsConfigured()
    {
        Assert.That(Provider(value: null).IsEnabled, Is.False);
    }

    [Test]
    public void RemoteAccessIsOnWhenExplicitlyEnabled()
    {
        Assert.That(Provider("true").IsEnabled, Is.True);
    }

    [Test]
    public void RemoteAccessEnablementIgnoresCasing()
    {
        Assert.That(Provider("TRUE").IsEnabled, Is.True);
    }

    [TestCase("false")]
    [TestCase("")]
    [TestCase("1")]
    [TestCase("yes")]
    public void OnlyTheWordTrueEnablesRemoteAccess(string value)
    {
        Assert.That(Provider(value).IsEnabled, Is.False);
    }

    private static McpRemoteAccessProvider Provider(string? value)
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_MCP_REMOTE_ENABLED"] = value })
            .Build());
}
