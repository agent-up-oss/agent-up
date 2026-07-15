namespace AgentUp.Installers.Features.Windows;

public sealed record WindowsInstallPayload(
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory);
