using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

public sealed class CapabilityIndexMergeProvider
{
    public NixEnvironmentSpec MergeEnvironments(IEnumerable<NixEnvironmentSpec> environments)
    {
        var list = environments.ToArray();
        var defaultNix = list.Select(item => item.DefaultNixPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        var rev = list.Select(item => item.NixpkgsRev).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var packages = list.SelectMany(item => item.Packages).Distinct(StringComparer.Ordinal).ToArray();
        return new NixEnvironmentSpec(defaultNix, rev, packages);
    }
}
