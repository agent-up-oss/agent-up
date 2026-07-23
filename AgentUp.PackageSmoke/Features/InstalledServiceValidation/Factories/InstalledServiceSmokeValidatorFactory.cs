using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;

public static class InstalledServiceSmokeValidatorFactory
{
    public static IInstalledServiceSmokeValidator Create(string platform, ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        => platform switch
        {
            "ubuntu" => new UbuntuInstalledServiceSmokeValidator(commands, serverProbe, securityChecks),
            "macos" => new SkippedInstalledServiceSmokeValidator("Skipping installed-service smoke for macOS because the .pkg installs only Agent-Up Installer.app; real service validation requires a noninteractive InstallerApp install mode."),
            "windows" => new WindowsInstalledServiceSmokeValidator(commands, serverProbe, securityChecks),
            "nixos" => new SkippedInstalledServiceSmokeValidator("Skipping installed-service smoke for NixOS because this CI job runs on Ubuntu with Nix, not a booted NixOS systemd host."),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
}
