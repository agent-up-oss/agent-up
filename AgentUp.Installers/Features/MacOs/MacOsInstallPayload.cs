namespace AgentUp.Installers.Features.MacOs;

public sealed record MacOsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory);
