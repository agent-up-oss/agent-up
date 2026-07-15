namespace AgentUp.Installers.Features.Windows;

public sealed record WindowsInstallerOptions(
    WindowsInstallPayload Payload,
    WindowsInstallerPaths Paths);
