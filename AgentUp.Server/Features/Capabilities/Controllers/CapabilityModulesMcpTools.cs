using System.ComponentModel;
using AgentUp.Server.Features.Capabilities.DTOs;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Capabilities.Controllers;

[McpServerToolType]
public sealed class CapabilityModulesMcpTools(CapabilityModulesController modules)
{
    [McpServerTool(Name = "list_capability_modules", Title = "List Capability Modules")]
    [Description("List capability packages in the Server local registry and whether this instance has enabled them for nix-shell wraps.")]
    public IReadOnlyList<CapabilityModuleDto> ListCapabilityModules() => modules.List();

    [McpServerTool(Name = "enable_capability_module", Title = "Enable Capability Module")]
    [Description("Enable a local-registry capability package on this Server so later application and agent launches wrap through nix.")]
    public CapabilityModuleDto EnableCapabilityModule(
        [Description("Capability package id, such as dotnet or node.")] string id,
        [Description("Package version. Optional; the latest local version is used when omitted.")] string? version = null)
        => modules.Enable(id, version);

    [McpServerTool(Name = "disable_capability_module", Title = "Disable Capability Module")]
    [Description("Stop wrapping later launches with this capability package. The package stays in the local registry.")]
    public CapabilityModuleDto DisableCapabilityModule(
        [Description("Capability package id to disable.")] string id)
        => modules.Disable(id);
}
