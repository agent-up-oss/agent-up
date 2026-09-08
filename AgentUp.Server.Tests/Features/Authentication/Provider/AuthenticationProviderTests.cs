using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public class AuthenticationProviderTests
{
    [Test]
    public void Login_AcceptsOnlyConfiguredPassword()
    {
        var service = Create(("AGENTUP_ADMIN_PASSWORD", "correct horse"));

        Assert.Multiple(() =>
        {
            Assert.That(service.Login("wrong"), Is.Null);
            Assert.That(service.IsAuthenticated(service.Login("correct horse")), Is.True);
        });
    }

    [Test]
    public void RequiresAuthenticationByDefault_WhenPasswordMissing()
    {
        var service = Create();
        Assert.Multiple(() =>
        {
            Assert.That(service.IsRequired, Is.True);
            Assert.That(service.IsAuthenticated(null), Is.False);
            Assert.That(service.Login("anything"), Is.Null);
        });
    }

    [Test]
    public void ExplicitDisableOverridesConfiguredPassword()
    {
        var service = Create(
            ("AGENTUP_ADMIN_PASSWORD", "secret"),
            ("AGENTUP_AUTH_DISABLED", "true"));
        Assert.Multiple(() =>
        {
            Assert.That(service.IsRequired, Is.False);
            Assert.That(service.Login("secret"), Is.Null);
        });
    }

    [Test]
    public void DisabledAuthentication_AcceptsRequestsWithoutToken()
    {
        var service = Create(("AGENTUP_AUTH_DISABLED", "true"));
        Assert.Multiple(() =>
        {
            Assert.That(service.IsRequired, Is.False);
            Assert.That(service.IsAuthenticated(null), Is.True);
        });
    }

    [Test]
    public void IsAuthenticated_RejectsExpiredSessions()
    {
        var service = Create(
            ("AGENTUP_ADMIN_PASSWORD", "secret"),
            ("AGENTUP_SESSION_LIFETIME_SECONDS", "-1"));
        var token = service.Login("secret");

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Not.Null);
            Assert.That(service.IsAuthenticated(token), Is.False);
        });
    }

    private static AuthenticationProvider Create(params (string Key, string Value)[] values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build());
}
