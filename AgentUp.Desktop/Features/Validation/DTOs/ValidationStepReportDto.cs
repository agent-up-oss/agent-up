namespace AgentUp.Desktop.Features.Validation.DTOs;

public sealed record ValidationStepReportDto(
    string Id,
    string Description,
    string? Action,
    ValidationTargetDto? Target,
    string? Value,
    DesktopResolvedTargetDto? Resolved,
    IReadOnlyList<DesktopTargetCandidateDto> Candidates,
    IReadOnlyList<ValidationExpectationReportDto> Expectations,
    bool Passed,
    long DurationMs,
    string Screenshot);
