using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Forge;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Services;

namespace AgentUp.Server.Features.Capabilities.Controllers;

public sealed class CapabilityModulesController(CapabilityModuleService modules)
{
    public IReadOnlyList<CapabilityModuleDto> List() => modules.List();

    public CapabilityChangeResult Enable(string id) => modules.Enable(id);

    public CapabilityChangeResult Disable(string id) => modules.Disable(id);

    public CapabilityChangeResult UpdateSettings(string id, IReadOnlyDictionary<string, string?> values)
        => modules.UpdateSettings(id, values);

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
        => modules.WrapLaunch(fileName, arguments);

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
        => modules.WrapModule(id, fileName, arguments);

    public CapabilityLaunchPlan? AgentLaunch(string id) => modules.AgentLaunch(id);

    public IRuntimeCapability? GetRuntime(string id) => modules.GetRuntime(id);

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => modules.ListRuntimes();

    public IReadOnlyList<IAgentCapability> ListAgents() => modules.ListAgents();

    public IReadOnlyList<IForgeCapability> ListForges() => modules.ListForges();

    public IForgeCapability? GetForge(string id) => modules.GetForge(id);

    public IReadOnlyList<string> RuntimePathPrefixes() => modules.RuntimePathPrefixes();

    public string? RuntimeRoot() => modules.RuntimeRoot();
}
