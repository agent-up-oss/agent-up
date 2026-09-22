namespace AgentUp.Desktop.Features.Applications.DTOs;

public sealed record CapabilityStatusDto(
    string CapabilityId,
    string? RequiredVersion,
    bool CanRun,
    IReadOnlyList<string> Messages);
