using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Services;

public sealed class GithubCapabilityPacker
{
    public const string PackageId = GithubForgeCapability.PackageId;
    public const string PackageVersion = GithubForgeCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
        => new()
        {
            SchemaVersion = "1",
            Id = PackageId,
            Version = PackageVersion,
            DisplayName = "GitHub",
            Publisher = "agent-up",
            Kind = "forge",
            Module = "AgentUp.Capabilities.Github.dll",
            Platforms = ["linux", "macos", "windows"]
        };

    public string DefaultNix() => "{ pkgs }: pkgs.mkShellNoCC { packages = []; }\n";
}
