using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

namespace AgentUp.Capabilities.Forgejo.Tests.Features.ForgejoCapability.Unit;

[TestFixture]
public sealed class ForgejoForgeCapabilityTests
{
    [Test]
    public void Identity_is_the_public_module_id()
    {
        var capability = new ForgejoForgeCapability();

        Assert.That(capability.Identity.Id, Is.EqualTo("forgejo"));
        Assert.That(capability.Identity.DisplayName, Is.EqualTo("Forgejo"));
    }

    [Test]
    public void Host_defaults_to_a_local_Forgejo_origin()
    {
        var capability = new ForgejoForgeCapability();

        Assert.That(capability.Host.ApiBaseUrl, Is.EqualTo("http://localhost:3000/api/v1/"));
        Assert.That(capability.Host.OAuthAuthorizeUrl, Is.EqualTo("http://localhost:3000/login/oauth/authorize"));
        Assert.That(capability.Host.OAuthTokenUrl, Is.EqualTo("http://localhost:3000/login/oauth/access_token"));
    }

    [Test]
    public void IsProtected_refuses_main_and_master()
    {
        Assert.That(ForgejoApiProvider.IsProtected("main"), Is.True);
        Assert.That(ForgejoApiProvider.IsProtected("refs/heads/master"), Is.True);
        Assert.That(ForgejoApiProvider.IsProtected("agent-topic"), Is.False);
    }
}
