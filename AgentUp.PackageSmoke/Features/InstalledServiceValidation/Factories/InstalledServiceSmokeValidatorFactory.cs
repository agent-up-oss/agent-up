using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;

public static class InstalledServiceSmokeValidatorFactory
{
    public static IInstalledServiceSmokeValidator Create(string platform, ICommandRunner commands, IServerProbe serverProbe)
    {
        var securityChecks = new RuntimeSecurityChecks(new SystemNetworkStateProvider(), new HttpClient());
        return platform switch
        {
            "ubuntu" => new UbuntuInstalledServiceSmokeValidator(commands, serverProbe, securityChecks),
            "macos" => new MacOsInstalledServiceSmokeValidator(commands, serverProbe, securityChecks),
            "windows" => new WindowsInstalledServiceSmokeValidator(commands, serverProbe, securityChecks),
            "nixos" => new SkippedInstalledServiceSmokeValidator("Skipping installed-service smoke for NixOS because this CI job runs on Ubuntu with Nix, not a booted NixOS systemd host."),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
    }
}
