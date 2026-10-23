using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Tests.Features.Forge.Unit;

[TestFixture]
public sealed class ForgeHostingDtoTests
{
    [Test]
    public void Record_carriesDetailAndRemedy()
    {
        var hosting = new ForgeHostingDto(
            ForgeHostingCopy.NotInstalled,
            null,
            null,
            [],
            [],
            null,
            ForgeHostingCopy.NotInstalledDetail,
            ForgeHostingCopy.NotInstalledRemedy);

        Assert.That(hosting.Availability, Is.EqualTo("notInstalled"));
        Assert.That(hosting.Detail, Does.Contain("forge capability"));
        Assert.That(hosting.Remedy, Does.Contain("capabilities bootstrap"));
    }

    [Test]
    public void LoginStart_carriesTheAuthorizationUrl()
        => Assert.That(
            new ForgeLoginStartDto("https://example.test/login/oauth/authorize").AuthorizationUrl,
            Does.Contain("authorize"));
}
