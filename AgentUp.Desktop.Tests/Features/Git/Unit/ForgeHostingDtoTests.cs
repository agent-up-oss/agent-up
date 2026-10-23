using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Git.Unit;

[TestFixture]
public sealed class ForgeHostingDtoTests
{
    [Test]
    public void Record_carriesDetailAndRemedy()
    {
        var hosting = DesktopDomain.Hosting().NotInstalled().Build();

        Assert.That(hosting.Availability, Is.EqualTo("notInstalled"));
        Assert.That(hosting.Detail, Does.Contain("forge capability"));
        Assert.That(hosting.Remedy, Does.Contain("capabilities bootstrap"));
    }

    [Test]
    public void Ready_omitsDetailAndRemedy()
    {
        var hosting = DesktopDomain.Hosting().Ready().Build();

        Assert.That(hosting.Availability, Is.EqualTo("ready"));
        Assert.That(hosting.Detail, Is.Null);
        Assert.That(hosting.Remedy, Is.Null);
        Assert.That(hosting.Proposal!.Title, Is.EqualTo("Ready"));
    }
}
