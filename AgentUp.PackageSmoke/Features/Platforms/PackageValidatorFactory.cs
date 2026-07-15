using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Platforms;

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
