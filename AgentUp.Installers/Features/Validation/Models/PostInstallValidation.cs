namespace AgentUp.Installers.Features.Validation.Models;

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

public static class PostInstallValidation
{
    public static ValidationReport Validate(InstalledState state, Version expectedVersion)
    {
        var findings = new List<ValidationFinding>();

        Require(state.ServiceRegistered, "service.missing", "Server service is not registered.", findings);
        Require(state.ServiceRunning, "service.stopped", "Server service is not running.", findings);
        Require(state.CliAvailableFromFreshShell, "cli.path", "CLI is not available from a new shell.", findings);
        Require(state.DesktopInstalled, "desktop.missing", "Desktop application is not installed in the expected location.", findings);

        ValidateVersion("installer.version", "Installer", state.InstallerVersion, expectedVersion, findings);
        ValidateVersion("cli.version", "CLI", state.CliVersion, expectedVersion, findings);
        ValidateVersion("server.version", "Server", state.ServerVersion, expectedVersion, findings);
        ValidateVersion("desktop.version", "Desktop", state.DesktopVersion, expectedVersion, findings);

        return new ValidationReport(findings);
    }

    private static void Require(bool condition, string code, string message, List<ValidationFinding> findings)
    {
        if (!condition)
            findings.Add(new ValidationFinding(code, message, ValidationSeverity.Error));
    }

    private static void ValidateVersion(
        string code,
        string component,
        Version? actual,
        Version expected,
        List<ValidationFinding> findings)
    {
        if (actual is null)
        {
            findings.Add(new ValidationFinding(code, $"{component} version could not be read.", ValidationSeverity.Error));
            return;
        }

        if (actual != expected)
        {
            findings.Add(new ValidationFinding(code, $"{component} version {actual} does not match installer version {expected}.", ValidationSeverity.Error));
        }
    }
}
