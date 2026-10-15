using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class CoveredDigestProviderTests
{
    [Test]
    public void Digest_isStableAcrossInsertionOrder()
    {
        var provider = new CoveredDigestProvider();
        var first = provider.Digest(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["b.cs"] = "sha256:2",
            ["a.cs"] = "sha256:1"
        });
        var second = provider.Digest(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.cs"] = "sha256:1",
            ["b.cs"] = "sha256:2"
        });

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Does.StartWith("sha256:"));
    }

    [Test]
    public void Digest_changesWhenAHashChanges()
    {
        var provider = new CoveredDigestProvider();
        var left = provider.Digest(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.cs"] = "sha256:1" });
        var right = provider.Digest(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.cs"] = "sha256:2" });

        Assert.That(left, Is.Not.EqualTo(right));
    }
}
