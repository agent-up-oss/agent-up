namespace AgentUp.Installers.Features.Ubuntu;

public sealed record UbuntuInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string ServiceFilePath,
    string IconPath);
