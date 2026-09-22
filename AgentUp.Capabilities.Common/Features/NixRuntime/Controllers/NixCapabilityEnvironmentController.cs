using AgentUp.Capabilities.Common.Features.NixRuntime.DTOs;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Services;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;

public sealed class NixCapabilityEnvironmentController(NixCapabilityEnvironmentService environment)
{
    public NixLaunchWrap Wrap(string fileName, IReadOnlyList<string> arguments, NixEnvironmentSpec spec)
        => environment.Wrap(fileName, arguments, spec);

    public NixEnvironmentSpec Merge(IReadOnlyList<NixEnvironmentSpec> environments)
        => environment.Merge(environments);
}
