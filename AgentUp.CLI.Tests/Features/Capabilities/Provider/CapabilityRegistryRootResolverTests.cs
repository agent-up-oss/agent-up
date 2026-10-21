using AgentUp.CLI.Features.Capabilities.Providers;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityRegistryRootResolverTests
{
    [Test]
    public void Resolve_usesTheConfiguredPathWhenSet()
    {
        var configured = Path.Join(Path.GetTempPath(), "agent-up-registry");

        Assert.That(
            CapabilityRegistryRootResolver.Resolve(configured, "/repo"),
            Is.EqualTo(Path.GetFullPath(configured)));
    }

    [Test]
    public void Resolve_defaultsToTheRepositoryDevRegistry()
    {
        Assert.That(
            CapabilityRegistryRootResolver.Resolve(null, "/repo"),
            Is.EqualTo(Path.GetFullPath(Path.Join("/repo", ".agent-up-dev", "capability-registry"))));
    }
}
