using AgentUp.Server.Composition;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class AgentUpServerManifestTests
{
    [Test]
    public void EnvironmentVariables_declaresSmokeValidationOptOut()
    {
        var manifest = new AgentUpServerManifest();

        Assert.That(manifest.EnvironmentVariables, Does.ContainKey("AGENTUP_AUTH_DISABLED"));
        Assert.That(manifest.EnvironmentVariables["AGENTUP_AUTH_DISABLED"], Is.EqualTo("true"));
    }
}
