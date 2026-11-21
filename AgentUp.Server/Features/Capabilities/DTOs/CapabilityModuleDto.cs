namespace AgentUp.Server.Features.Capabilities.DTOs;

/// <param name="Available">Whether the software the capability runs was found on the Server host. A capability that is not available cannot be enabled.</param>
/// <param name="State">One of <see cref="CapabilityModuleStates"/>.</param>
/// <param name="Missing">What was not found: <c>nix</c>, or an executable name.</param>
/// <param name="Settings">What a user configures once the capability is enabled.</param>
public sealed record CapabilityModuleDto(
    string Id,
    string Version,
    string DisplayName,
    string Publisher,
    string Kind,
    bool Enabled,
    bool Available,
    string State,
    bool CanRun,
    IReadOnlyList<string> Missing,
    IReadOnlyList<CapabilitySettingDto> Settings,
    IReadOnlyList<string> Messages);
