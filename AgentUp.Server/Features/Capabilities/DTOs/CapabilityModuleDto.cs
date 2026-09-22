namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record CapabilityModuleDto(
    string Id,
    string Version,
    string DisplayName,
    string Publisher,
    string Kind,
    bool Enabled,
    string State,
    bool CanRun,
    IReadOnlyList<string> Messages);
