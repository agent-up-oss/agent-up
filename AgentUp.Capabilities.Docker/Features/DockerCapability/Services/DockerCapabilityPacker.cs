using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

public sealed class DockerCapabilityPacker
{
    public const string PackageId = DockerRuntimeCapability.PackageId;
    public const string PackageVersion = DockerRuntimeCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
    {
        var runtime = new DockerRuntimeCapability();
        return new()
        {
            SchemaVersion = "1",
            Id = runtime.Identity.Id,
            Version = runtime.Identity.PackageVersion,
            DisplayName = runtime.Identity.DisplayName,
            Publisher = runtime.Identity.Publisher,
            Kind = CapabilityKind.Runtime,
            Module = "AgentUp.Capabilities.Docker.dll",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = FirstPartyNixPin.Nixpkgs,
                Packages = runtime.NixPackages.Select(package => package.Package).ToArray()
            },
            Provides = ["docker"],
            Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["image"] = new() { Required = true, Type = "string" }
            },
            Probe = new CapabilityProbeTemplate { Command = "docker", Arguments = ["--version"] }
        };
    }

    public string DefaultNix()
        => FirstPartyNixPin.DefaultNix(new DockerRuntimeCapability().NixPackages.Select(package => package.Package).ToArray());
}
