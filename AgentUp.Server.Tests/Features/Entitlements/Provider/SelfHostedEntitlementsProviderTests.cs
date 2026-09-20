using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Entitlements.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Entitlements.Provider;

[TestFixture]
public sealed class SelfHostedEntitlementsProviderTests
{
    [Test]
    public void ForSubject_DoesNotExpire()
    {
        var dto = new SelfHostedEntitlementsProvider(new ConfigurationBuilder().Build()).ForSubject("admin");
        Assert.That(dto.ExpiresAt, Is.Null);
    }

    [Test]
    public void ForSubject_LeavesLimitsEmpty()
    {
        var dto = new SelfHostedEntitlementsProvider(new ConfigurationBuilder().Build()).ForSubject("admin");
        Assert.Multiple(() =>
        {
            Assert.That(dto.Limits, Is.Empty);
            Assert.That(dto.Features[OperationPermissions.WorkspaceCreate].Available, Is.True);
        });
    }
}
