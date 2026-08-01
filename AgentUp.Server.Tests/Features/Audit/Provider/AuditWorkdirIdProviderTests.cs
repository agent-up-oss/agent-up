using AgentUp.Server.Features.Audit.Providers;

namespace AgentUp.Server.Tests.Features.Audit.Provider;

[TestFixture]
public sealed class AuditWorkdirIdProviderTests
{
    [Test]
    public void Create_ReturnsStableOpaqueId()
    {
        var provider = new AuditWorkdirIdProvider();

        var first = provider.Create("/tmp/repo");
        var second = provider.Create("/tmp/repo");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(16));
            Assert.That(first, Does.Not.Contain("/tmp/repo"));
        });
    }
}
