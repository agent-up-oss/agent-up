using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class CredentialValidationServiceTests
{
    [Test]
    public void DisabledMode_ReturnsUnrestrictedPrincipalWithoutAToken()
    {
        var service = Create(("AGENTUP_AUTH_DISABLED", "true"));
        var principal = service.Validate(null);
        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Permissions, Is.EqualTo(OperationPermissions.All));
        });
    }

    [Test]
    public void LocalAdministratorMode_RejectsMissingToken()
    {
        var service = Create(("AGENTUP_ADMIN_PASSWORD", "secret"));
        Assert.That(service.Validate(null), Is.Null);
    }

    private static CredentialValidationService Create(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new CredentialValidationService(
            new AuthenticationModeProvider(configuration),
            new LocalAdministratorCredentialValidator(new AuthenticationProvider(configuration)),
            new ExternalBearerCredentialValidator(configuration));
    }
}
