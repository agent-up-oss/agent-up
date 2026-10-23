using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

public sealed class ForgejoCapabilityPacker
{
    public const string PackageId = ForgejoForgeCapability.PackageId;
    public const string PackageVersion = ForgejoForgeCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
        => new()
        {
            SchemaVersion = "1",
            Id = PackageId,
            Version = PackageVersion,
            DisplayName = "Forgejo",
            Publisher = "agent-up",
            Kind = "forge",
            Module = "AgentUp.Capabilities.Forgejo.dll",
            Platforms = ["linux", "macos", "windows"]
        };

    public string DefaultNix() => "{ pkgs }: pkgs.mkShellNoCC { packages = []; }\n";
}
