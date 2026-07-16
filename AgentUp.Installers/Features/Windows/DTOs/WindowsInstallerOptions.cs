using AgentUp.Installers.Features.Windows.Models;

namespace AgentUp.Installers.Features.Windows.DTOs;

public sealed record WindowsInstallerOptions(
    WindowsInstallPayload Payload,
    WindowsInstallerPaths Paths);
