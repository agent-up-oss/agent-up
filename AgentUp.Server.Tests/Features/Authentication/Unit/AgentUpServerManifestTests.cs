using AgentUp.Server.Composition;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class AgentUpServerManifestTests
{
    [Test]
    public void EnvironmentVariables_doesNotShipAuthDisabledByDefault()
    {
        var manifest = new AgentUpServerManifest();

        Assert.That(manifest.EnvironmentVariables, Does.Not.ContainKey("AGENTUP_AUTH_DISABLED"));
    }

    [Test]
    public void EnvironmentVariables_doesNotShipSentryDsnByDefault()
    {
        var manifest = new AgentUpServerManifest();

        Assert.That(manifest.EnvironmentVariables, Does.Not.ContainKey("SENTRY_DSN"));
    }
}
