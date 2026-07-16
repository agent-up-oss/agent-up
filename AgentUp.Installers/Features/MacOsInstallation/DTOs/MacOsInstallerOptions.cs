using AgentUp.Installers.Features.MacOsInstallation.Models;

namespace AgentUp.Installers.Features.MacOsInstallation.DTOs;

public sealed record MacOsInstallerOptions(
    MacOsInstallPayload Payload,
    MacOsInstallerPaths Paths);
