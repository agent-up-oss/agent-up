namespace AgentUp.Installers.Features.MacOs.DTOs;

public sealed record MacOsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory);
