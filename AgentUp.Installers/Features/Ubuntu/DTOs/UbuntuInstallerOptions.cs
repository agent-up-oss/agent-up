using AgentUp.Installers.Features.Ubuntu.Models;

namespace AgentUp.Installers.Features.Ubuntu.DTOs;

public sealed record UbuntuInstallerOptions(
    UbuntuInstallPayload Payload,
    UbuntuInstallerPaths Paths);
