using AgentUp.Installers.Features.Installation.DTOs;

namespace AgentUp.Installers.Features.Installation.Models;

public static class PostInstallValidation
{
    public static ValidationReport Validate(InstalledState state, Version expectedVersion)
    {
        var findings = new List<ValidationFinding>();

        Require(state.ServiceRegistered, "service.missing", "Server service is not registered.", findings);
        Require(state.ServiceRunning, "service.stopped", "Server service is not running.", findings);
        Require(state.CliAvailableFromFreshShell, "cli.path", "CLI is not available from a new shell.", findings);
        Require(state.DesktopInstalled, "desktop.missing", "Desktop application is not installed in the expected location.", findings);

        if (state.TrayInstalled || state.TrayAutoStartRegistered || state.TrayVersion is not null)
        {
            Require(state.TrayInstalled, "tray.missing", "Tray application is not installed in the expected location.", findings);
            Require(state.TrayAutoStartRegistered, "tray.autostart", "Tray auto-start is not registered for the current user.", findings);
        }

        ValidateVersion("installer.version", "Installer", state.InstallerVersion, expectedVersion, findings);
        ValidateVersion("cli.version", "CLI", state.CliVersion, expectedVersion, findings);
        ValidateVersion("server.version", "Server", state.ServerVersion, expectedVersion, findings);
        ValidateVersion("desktop.version", "Desktop", state.DesktopVersion, expectedVersion, findings);
        if (state.TrayVersion is not null)
            ValidateVersion("tray.version", "Tray", state.TrayVersion, expectedVersion, findings);

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
