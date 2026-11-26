using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentModelProxyAccessTests
{
    [Test]
    public void Matches_acceptsOnlyTheIssuedKey()
    {
        var access = new AgentModelProxyAccess();

        Assert.Multiple(() =>
        {
            Assert.That(access.Key, Has.Length.EqualTo(64));
            Assert.That(access.Matches(access.Key), Is.True);
            Assert.That(access.Matches(access.Key.ToUpperInvariant()), Is.False);
            Assert.That(access.Matches("0".PadLeft(64, '0')), Is.False);
            Assert.That(access.Matches(null), Is.False);
        });
    }
}
