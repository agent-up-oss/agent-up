using AgentUp.Capabilities.Common.Features.NixRuntime.DTOs;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Services;

public sealed class NixCapabilityEnvironmentService(
    NixCapabilityCommandBuilder builder,
    CapabilityIndexMergeProvider merge)
{
    public NixLaunchWrap Wrap(string fileName, IReadOnlyList<string> arguments, NixEnvironmentSpec environment)
        => builder.Wrap(fileName, arguments, environment);

    public NixEnvironmentSpec Merge(IReadOnlyList<NixEnvironmentSpec> environments)
        => merge.MergeEnvironments(environments);
}
