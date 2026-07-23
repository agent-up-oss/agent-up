namespace AgentUp.Installers.Features.Installation.DTOs;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ValidationFinding(string Code, string Message, ValidationSeverity Severity);

public sealed record InstalledState(
    bool ServiceRegistered,
    bool ServiceRunning,
    bool CliAvailableFromFreshShell,
    bool DesktopInstalled,
    Version? InstallerVersion,
    Version? CliVersion,
    Version? ServerVersion,
    Version? DesktopVersion);

public sealed record ValidationReport(IReadOnlyList<ValidationFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != ValidationSeverity.Error);
}
