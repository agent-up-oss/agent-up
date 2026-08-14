namespace LocalInstaller.Core.Features.Installation.Models;

public sealed record ProductComponent(
    string Id,
    string DisplayName,
    string Description = "",
    InstallerComponentTarget? Target = null,
    string? ExecutableName = null,
    string? PayloadDirectoryName = null,
    string? SourceProjectPath = null)
{
    public static ProductComponent Desktop
        => new("desktop", "Desktop", "Desktop application.", InstallerComponentTarget.Desktop, "desktop", "desktop");

    public static ProductComponent Server
        => new("server", "Server", "Local service application.", InstallerComponentTarget.Server, "server", "server");

    public static ProductComponent Cli
        => new("cli", "CLI", "Command-line application.", InstallerComponentTarget.Cli, "cli", "cli");

    public static ProductComponent Tray
        => new("tray", "Tray", "Notification area application.", InstallerComponentTarget.Tray, "tray", "tray");
}
