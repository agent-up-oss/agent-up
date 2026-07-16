namespace AgentUp.Installers.Features.Windows.DTOs;

public sealed record WindowsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory);
