using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Factories;

public static class PackageValidatorFactory
{
    public static IPackageValidator Create(string platform, ICommandRunner commands)
        => platform switch
        {
            "ubuntu" => new UbuntuPackageValidator(new UbuntuPackageArchiveProvider(commands)),
            "macos" => new MacOsPackageValidator(new MacOsPackageArchiveProvider(commands)),
            "windows" => new WindowsPackageValidator(new WindowsPackageArchiveProvider(commands)),
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };
}
