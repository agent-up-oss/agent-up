using AgentUp.Sdk.Common;

namespace AgentUp.Server.Features.Capabilities.Interfaces;

public interface ICapabilitySoftwareProbe
{
    /// <summary>
    /// What cannot be found, in declaration order: <c>nix</c> for software the capability
    /// delivers through Nix, otherwise the executable name itself. Empty when everything is found.
    /// </summary>
    IReadOnlyList<string> Missing(IReadOnlyList<CapabilitySoftware> software);
}
