using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

public sealed class CapabilityIndexMergeProvider
{
    public CapabilityRegistryIndex Merge(params CapabilityRegistryIndex[] indexes)
    {
        var packages = indexes
            .SelectMany(index => index.Packages)
            .GroupBy(entry => entry.Id + "@" + entry.Version, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Version, StringComparer.Ordinal)
            .ToArray();
        return new CapabilityRegistryIndex { SchemaVersion = "1", Packages = packages };
    }

    public NixEnvironmentSpec MergeEnvironments(IEnumerable<NixEnvironmentSpec> environments)
    {
        var list = environments.ToArray();
        var defaultNix = list.Select(item => item.DefaultNixPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        var rev = list.Select(item => item.NixpkgsRev).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var packages = list.SelectMany(item => item.Packages).Distinct(StringComparer.Ordinal).ToArray();
        return new NixEnvironmentSpec(defaultNix, rev, packages);
    }
}
