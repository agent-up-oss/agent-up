using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDiscovery.Provider;

[TestFixture]
public sealed class CapabilityCliCandidateFactoryTests
{
    [Test]
    public void FromDeclaredCommand_returnsEmptyWhenCommandIsMissing()
    {
        Assert.That(CapabilityCliCandidateFactory.FromDeclaredCommand(null, ["acp"], ["--version"]), Is.Empty);
        Assert.That(CapabilityCliCandidateFactory.FromDeclaredCommand("  ", null, null), Is.Empty);
    }

    [Test]
    public void FromDeclaredCommand_usesDeclaredLaunchAndDefaultsVersionArguments()
    {
        var candidate = CapabilityCliCandidateFactory.FromDeclaredCommand("/opt/agent", ["acp"], null).Single();

        Assert.Multiple(() =>
        {
            Assert.That(candidate.FileName, Is.EqualTo("/opt/agent"));
            Assert.That(candidate.LaunchArguments, Is.EqualTo(new[] { "acp" }));
            Assert.That(candidate.VersionArguments, Is.EqualTo(new[] { "--version" }));
        });
    }
}
