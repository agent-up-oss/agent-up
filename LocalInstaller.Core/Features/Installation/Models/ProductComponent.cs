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
        => new("desktop", "Desktop", "Human UI for managed workspaces.", InstallerComponentTarget.Desktop, "AgentUp.Desktop", "desktop", "AgentUp.Desktop/AgentUp.Desktop.csproj");

    public static ProductComponent Server
        => new("server", "Server", "Local runtime authority, API service, and tray app.", InstallerComponentTarget.Server, "AgentUp.Server", "server", "AgentUp.Server/AgentUp.Server.csproj");

    public static ProductComponent Cli
        => new("cli", "CLI", "Terminal command wrapper for the local Server.", InstallerComponentTarget.Cli, "AgentUp.CLI", "cli", "AgentUp.CLI/AgentUp.CLI.csproj");

    public static ProductComponent Tray
        => new("tray", "Tray", "Notification area app.", InstallerComponentTarget.Tray, "AgentUp.Tray", "tray", "AgentUp.Tray/AgentUp.Tray.csproj");
}
