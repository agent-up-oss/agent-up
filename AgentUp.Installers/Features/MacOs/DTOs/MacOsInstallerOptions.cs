using AgentUp.Installers.Features.MacOs.Models;

namespace AgentUp.Installers.Features.MacOs.DTOs;

public sealed record MacOsInstallerOptions(
    MacOsInstallPayload Payload,
    MacOsInstallerPaths Paths);
