using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Services;

namespace AgentUp.Server.Features.Capabilities.Controllers;

public sealed class CapabilityModulesController(CapabilityModuleService modules)
{
    public IReadOnlyList<CapabilityModuleDto> List() => modules.List();

    public CapabilityModuleDto Enable(string id, string? version) => modules.Enable(id, version);

    public CapabilityModuleDto Disable(string id) => modules.Disable(id);

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
        => modules.WrapLaunch(fileName, arguments);

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
        => modules.WrapModule(id, fileName, arguments);

    public CapabilityLaunchPlan? AgentLaunch(string id) => modules.AgentLaunch(id);

    public IRuntimeCapability? GetRuntime(string id) => modules.GetRuntime(id);

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => modules.ListRuntimes();

    public IReadOnlyList<IAgentCapability> ListAgents() => modules.ListAgents();

    public IReadOnlyList<string> RuntimePathPrefixes() => modules.RuntimePathPrefixes();

    public string? RuntimeRoot() => modules.RuntimeRoot();
}
