using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class AuthenticationServiceTests
{
    [Test]
    public void Login_ReturnsDisabledStatusWithoutAToken_WhenAuthenticationIsDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_AUTH_DISABLED"] = "true" }).Build();
        var service = new AuthenticationService(new AuthenticationProvider(configuration));

        var result = service.Login("unused");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AuthenticationRequired, Is.False);
            Assert.That(result.AccessToken, Is.Null);
        });
    }
}
