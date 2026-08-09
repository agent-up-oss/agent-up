namespace LocalInstaller.Core.Features.UbuntuInstallation.DTOs;

public sealed record UbuntuInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string TrayDirectory,
    string ServiceFilePath,
    string IconPath);
