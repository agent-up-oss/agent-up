using AgentUp.Sdk.Forge;

namespace AgentUp.Sdk.Forge.Tests.Features.ForgeSdk.Unit;

[TestFixture]
public sealed class ForgeHostConfigurationTests
{
    [Test]
    public void Host_carries_module_owned_endpoints()
    {
        var host = new ForgeHostConfiguration(
            "https://git.example.test/api/v1/",
            "https://git.example.test/login/oauth/authorize",
            "https://git.example.test/login/oauth/access_token");

        Assert.That(host.ApiBaseUrl, Does.EndWith("/api/v1/"));
        Assert.That(host.OAuthAuthorizeUrl, Does.Contain("authorize"));
        Assert.That(host.OAuthTokenUrl, Does.Contain("access_token"));
    }
}
