using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Provider;

[TestFixture]
public sealed class CapabilityIndexMergeProviderTests
{
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
