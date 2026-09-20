using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class AuthenticationModeProviderTests
{
    [Test]
    public void DefaultsToLocalAdministrator()
    {
        var provider = new AuthenticationModeProvider(new ConfigurationBuilder().Build());
        Assert.That(provider.Current.ToString(), Is.EqualTo("LocalAdministrator"));
    }

    [Test]
    public void DisabledFlagSelectsDisabledMode()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_AUTH_DISABLED"] = "true" }).Build();
        Assert.That(new AuthenticationModeProvider(configuration).Current.ToString(), Is.EqualTo("Disabled"));
    }

    [Test]
    public void ExternalBearerModeIsSelectedFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_AUTH_MODE"] = "externalBearer" }).Build();
        Assert.That(new AuthenticationModeProvider(configuration).Current.ToString(), Is.EqualTo("ExternalBearer"));
    }
}
