using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Factories;

public static class PackageValidatorFactory
{
    public static IPackageValidator Create(string platform, ICommandRunner commands)
        => platform switch
        {
            "ubuntu" => new UbuntuPackageValidator(commands),
            "macos" => new MacOsPackageValidator(commands),
            "windows" => new WindowsPackageValidator(commands),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
}
