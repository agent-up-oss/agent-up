using LocalInstaller.Core.Features.WindowsInstallation.Models;

namespace LocalInstaller.Core.Features.WindowsInstallation.DTOs;

public sealed record WindowsInstallerOptions(
    WindowsInstallPayload Payload,
    WindowsInstallerPaths Paths);
