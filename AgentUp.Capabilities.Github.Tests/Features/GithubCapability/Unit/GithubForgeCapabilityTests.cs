using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Services;

namespace AgentUp.Capabilities.Github.Tests.Features.GithubCapability.Unit;

[TestFixture]
public sealed class GithubForgeCapabilityTests
{
    [Test]
    public void Identity_is_the_public_module_id()
    {
        var capability = new GithubForgeCapability();

        Assert.That(capability.Identity.Id, Is.EqualTo("github"));
        Assert.That(capability.Identity.DisplayName, Is.EqualTo("GitHub"));
    }

    [Test]
    public void Host_defaults_to_github_dot_com_endpoints()
    {
        var capability = new GithubForgeCapability();

        Assert.That(capability.Host.ApiBaseUrl, Is.EqualTo(GithubHostConfigurationProvider.DefaultApiBaseUrl));
        Assert.That(capability.Host.OAuthAuthorizeUrl, Is.EqualTo(GithubHostConfigurationProvider.DefaultAuthorizeUrl));
        Assert.That(capability.Host.OAuthTokenUrl, Is.EqualTo(GithubHostConfigurationProvider.DefaultTokenUrl));
    }

    [Test]
    public void IsProtected_refuses_main_and_master()
    {
        Assert.That(GithubApiProvider.IsProtected("main"), Is.True);
        Assert.That(GithubApiProvider.IsProtected("refs/heads/master"), Is.True);
        Assert.That(GithubApiProvider.IsProtected("agent-topic"), Is.False);
    }
}
