namespace AgentUp.Installers.Features.Ubuntu;

public sealed record UbuntuInstallerOptions(
    UbuntuInstallPayload Payload,
    UbuntuInstallerPaths Paths);
