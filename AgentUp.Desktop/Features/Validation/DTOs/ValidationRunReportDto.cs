namespace AgentUp.Desktop.Features.Validation.DTOs;

public sealed record ValidationRunReportDto(
    string FlowId,
    string Name,
    string Surface,
    bool Passed,
    string Message,
    DateTimeOffset StartedAtUtc,
    long DurationMs,
    IReadOnlyList<ValidationStepReportDto> Steps);
