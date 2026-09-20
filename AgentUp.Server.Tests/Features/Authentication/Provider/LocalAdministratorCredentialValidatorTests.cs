using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class LocalAdministratorCredentialValidatorTests
{
    [Test]
    public void Validate_ReturnsAdminPrincipalForIssuedSession()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "secret" }).Build();
        var sessions = new AuthenticationProvider(configuration);
        var validator = new LocalAdministratorCredentialValidator(sessions);
        var principal = validator.Validate(sessions.Login("secret"));

        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Subject, Is.EqualTo("admin"));
            Assert.That(principal.Permissions, Is.EqualTo(OperationPermissions.All));
        });
    }

    [Test]
    public void Validate_RejectsUnknownToken()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "secret" }).Build();
        var validator = new LocalAdministratorCredentialValidator(new AuthenticationProvider(configuration));
        Assert.That(validator.Validate("nope"), Is.Null);
    }
}
