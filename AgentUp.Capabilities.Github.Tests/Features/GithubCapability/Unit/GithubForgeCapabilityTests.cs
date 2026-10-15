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
    public void IsProtected_refuses_main_and_master()
    {
        Assert.That(GithubApiProvider.IsProtected("main"), Is.True);
        Assert.That(GithubApiProvider.IsProtected("refs/heads/master"), Is.True);
        Assert.That(GithubApiProvider.IsProtected("agent-topic"), Is.False);
    }
}
