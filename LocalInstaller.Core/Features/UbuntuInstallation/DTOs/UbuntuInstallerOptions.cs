using LocalInstaller.Core.Features.UbuntuInstallation.Models;

namespace LocalInstaller.Core.Features.UbuntuInstallation.DTOs;

public sealed record UbuntuInstallerOptions(
    UbuntuInstallPayload Payload,
    UbuntuInstallerPaths Paths,
    UbuntuInstallerManifest Manifest);
