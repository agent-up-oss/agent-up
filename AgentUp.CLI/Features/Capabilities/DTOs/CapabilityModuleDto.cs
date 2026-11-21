namespace AgentUp.CLI.Features.Capabilities.DTOs;

/// <param name="Available">Whether the software the capability runs was found on the Server host.</param>
/// <param name="Missing">What was not found: <c>nix</c>, or an executable name.</param>
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
    IReadOnlyList<string> Messages);
