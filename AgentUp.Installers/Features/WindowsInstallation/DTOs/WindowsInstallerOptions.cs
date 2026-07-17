using AgentUp.Installers.Features.WindowsInstallation.Models;

namespace AgentUp.Installers.Features.WindowsInstallation.DTOs;

public sealed record WindowsInstallerOptions(
    WindowsInstallPayload Payload,
    WindowsInstallerPaths Paths);
