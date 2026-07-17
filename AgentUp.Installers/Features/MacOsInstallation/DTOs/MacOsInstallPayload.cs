namespace AgentUp.Installers.Features.MacOsInstallation.DTOs;

public sealed record MacOsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory);
