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
    Version? DesktopVersion)
{
    public bool TrayInstalled { get; init; } = false;
    public bool TrayAutoStartRegistered { get; init; } = false;
    public Version? TrayVersion { get; init; } = null;
}

public sealed record ValidationReport(IReadOnlyList<ValidationFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != ValidationSeverity.Error);
}
