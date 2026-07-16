using AgentUp.Installers.Features.UbuntuInstallation.Models;

namespace AgentUp.Installers.Features.UbuntuInstallation.DTOs;

public sealed record UbuntuInstallerOptions(
    UbuntuInstallPayload Payload,
    UbuntuInstallerPaths Paths);
