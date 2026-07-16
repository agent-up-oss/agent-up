using AgentUp.PackageSmoke.Features.Platforms.Services;
using AgentUp.PackageSmoke.Features.Validation.Providers;
using AgentUp.PackageSmoke.Features.Validation.Services;

namespace AgentUp.PackageSmoke.Features.Platforms.Providers;

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
