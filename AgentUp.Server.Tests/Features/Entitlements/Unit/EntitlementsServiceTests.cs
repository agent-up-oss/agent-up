using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Entitlements.Providers;
using AgentUp.Server.Features.Entitlements.Services;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Entitlements.Unit;

[TestFixture]
public sealed class EntitlementsServiceTests
{
    [Test]
    public void ForSubject_BindsTheAuthenticatedSubject()
    {
        var dto = Create().ForSubject("admin");
        Assert.That(dto.Subject, Is.EqualTo("admin"));
    }

    [Test]
    public void ForSubject_UsesConfiguredConnectionId()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_CONNECTION_ID"] = "box-1" }).Build();
        var dto = new EntitlementsService(new SelfHostedEntitlementsProvider(configuration)).ForSubject("admin");
        Assert.That(dto.ConnectionId, Is.EqualTo("box-1"));
    }

    private static EntitlementsService Create()
        => new(new SelfHostedEntitlementsProvider(new ConfigurationBuilder().Build()));
}
