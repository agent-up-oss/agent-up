namespace AgentUp.Verification.Features.Verification.DTOs;

/// <summary>One parsed <c>au-check</c> invocation. The parser fills this; the command service does not read argv.</summary>
public sealed record AuCheckInvocation(
    string Command,
    string? CheckId,
    string RepositoryRoot,
    string ConfigPath,
    bool ConfigExplicit,
    string? ReportDirectory,
    bool RunMissing,
    double? Minimum,
    string? Error);
