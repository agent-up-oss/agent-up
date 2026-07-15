namespace AgentUp.Installers.Features.MacOs;

public sealed record MacOsInstallerOptions(
    MacOsInstallPayload Payload,
    MacOsInstallerPaths Paths);
