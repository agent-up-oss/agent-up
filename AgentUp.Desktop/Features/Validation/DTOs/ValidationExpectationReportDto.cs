namespace AgentUp.Desktop.Features.Validation.DTOs;

public sealed record ValidationExpectationReportDto(
    string Kind,
    string Value,
    bool Passed,
    string? Actual,
    string? Error);
