namespace AgentUp.Server.Features.Diagnostics.DTOs;

public sealed record ApplicationDiagnosticsDto(
    string Name,
    string ProcessState,
    string? Health,
    IReadOnlyList<string> Logs,
    bool LogsTruncated);
