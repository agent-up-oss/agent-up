using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class AuthenticationServiceTests
{
    [Test]
    public void Login_ReturnsDisabledStatusWithoutAToken_WhenAuthenticationIsDisabled()
    {
        var service = Create(("AGENTUP_AUTH_DISABLED", "true"));

        var result = service.Login(new LoginRequest("unused"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AuthenticationRequired, Is.False);
            Assert.That(result.AccessToken, Is.Null);
        });
    }

    [Test]
    public void Login_RejectsPasswordWhenExternalBearerModeIsConfigured()
    {
        var service = Create(
            ("AGENTUP_AUTH_MODE", "externalBearer"),
            ("AGENTUP_ADMIN_PASSWORD", "secret"));

        Assert.That(service.Login(new LoginRequest("secret")), Is.Null);
    }

    private static AuthenticationService Create(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new AuthenticationService(new AuthenticationProvider(configuration), new AuthenticationModeProvider(configuration));
    }
}
