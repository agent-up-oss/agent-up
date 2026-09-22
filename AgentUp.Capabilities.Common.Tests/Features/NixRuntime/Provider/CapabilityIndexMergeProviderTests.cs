using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Provider;

[TestFixture]
public sealed class CapabilityIndexMergeProviderTests
{
    [Test]
    public void Merge_keeps_the_first_duplicate_package()
    {
        var first = new CapabilityRegistryIndex
        {
            Packages = [new CapabilityRegistryIndexEntry("dotnet", "1.0.0", ".NET", "agent-up", "runtime")]
        };
        var second = new CapabilityRegistryIndex
        {
            Packages = [new CapabilityRegistryIndexEntry("dotnet", "1.0.0", "Other", "other", "runtime")]
        };

        var merged = new CapabilityIndexMergeProvider().Merge(first, second);

        Assert.That(merged.Packages.Single().DisplayName, Is.EqualTo(".NET"));
    }

    [Test]
    public void MergeEnvironments_unions_packages_and_keeps_the_first_default_nix()
    {
        var merged = new CapabilityIndexMergeProvider().MergeEnvironments(
        [
            new NixEnvironmentSpec("/a/default.nix", "rev-a", ["dotnet-sdk_10"]),
            new NixEnvironmentSpec("/b/default.nix", "rev-b", ["docker"])
        ]);

        Assert.That(merged.DefaultNixPath, Is.EqualTo("/a/default.nix"));
        Assert.That(merged.Packages, Is.EqualTo(new[] { "dotnet-sdk_10", "docker" }));
    }
}
