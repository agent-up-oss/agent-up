using System.ComponentModel;
using AgentUp.Server.Features.Capabilities.DTOs;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Capabilities.Controllers;

[McpServerToolType]
public sealed class CapabilityModulesMcpTools(CapabilityModulesController modules)
{
    [McpServerTool(Name = "list_capability_modules", Title = "List Capability Modules")]
    [Description("List the capabilities built into this Server, whether each is enabled, and whether the software it runs was found.")]
    public IReadOnlyList<CapabilityModuleDto> ListCapabilityModules() => modules.List();

    [McpServerTool(Name = "enable_capability_module", Title = "Enable Capability Module")]
    [Description("Enable a built-in capability on this Server. Refused while the software it runs cannot be found on the Server host.")]
    public CapabilityChangeResult EnableCapabilityModule(
        [Description("Capability id, such as dotnet or opencode.")] string id)
        => modules.Enable(id);

    [McpServerTool(Name = "disable_capability_module", Title = "Disable Capability Module")]
    [Description("Stop offering a built-in capability's feature. The capability stays installed.")]
    public CapabilityChangeResult DisableCapabilityModule(
        [Description("Capability id to disable.")] string id)
        => modules.Disable(id);
}
