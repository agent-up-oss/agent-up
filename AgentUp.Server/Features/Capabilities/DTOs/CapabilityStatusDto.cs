namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record CapabilityStatusDto(
    string CapabilityId,
    string? RequiredVersion,
    bool CanRun,
    IReadOnlyList<string> Messages);
