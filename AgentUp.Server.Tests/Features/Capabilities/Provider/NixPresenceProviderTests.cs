using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class NixPresenceProviderTests
{
    [Test]
    public void Injected_detector_marks_nix_available()
    {
        var provider = new NixPresenceProvider(() => true);

        Assert.That(provider.IsAvailable, Is.EqualTo(!OperatingSystem.IsWindows()));
    }

    [Test]
    public void Injected_detector_can_report_nix_missing()
    {
        var provider = new NixPresenceProvider(() => false);

        Assert.That(provider.IsAvailable, Is.False);
    }
}
