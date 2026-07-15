using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstalledServices;

public static class InstalledServiceSmokeValidatorFactory
{
    public static IInstalledServiceSmokeValidator Create(string platform, ICommandRunner commands, IServerProbe serverProbe)
        => platform switch
        {
            "ubuntu" => new UbuntuInstalledServiceSmokeValidator(commands, serverProbe),
            "macos" => new MacOsInstalledServiceSmokeValidator(commands, serverProbe),
            "windows" => new WindowsInstalledServiceSmokeValidator(commands, serverProbe),
            "nixos" => new SkippedInstalledServiceSmokeValidator("Skipping installed-service smoke for NixOS because this CI job runs on Ubuntu with Nix, not a booted NixOS systemd host."),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
}
