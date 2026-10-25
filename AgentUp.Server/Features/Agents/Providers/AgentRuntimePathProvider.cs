using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Capabilities.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentRuntimePathProvider(
    IEnumerable<CapabilityModulesController> modules) : IAgentRuntimePathProvider
{
    private CapabilityModulesController? Modules => modules.FirstOrDefault();

    public IReadOnlyList<string> PathPrefixes() => Modules?.RuntimePathPrefixes() ?? [];

    public string? RuntimeRoot() => Modules?.RuntimeRoot();
}
