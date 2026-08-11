namespace LocalInstaller.Core.Features.WindowsInstallation.DTOs;

public sealed record WindowsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string TrayDirectory);
