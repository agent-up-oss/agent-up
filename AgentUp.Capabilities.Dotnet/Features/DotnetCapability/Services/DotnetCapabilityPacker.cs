using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Sdk.Common;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

public sealed class DotnetCapabilityPacker
{
    public const string PackageId = DotnetRuntimeCapability.PackageId;
    public const string PackageVersion = DotnetRuntimeCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
    {
        var runtime = new DotnetRuntimeCapability();
        return new()
        {
            SchemaVersion = "1",
            Id = runtime.Identity.Id,
            Version = runtime.Identity.PackageVersion,
            DisplayName = runtime.Identity.DisplayName,
            Publisher = runtime.Identity.Publisher,
            Kind = CapabilityKind.Runtime,
            Module = "AgentUp.Capabilities.Dotnet.dll",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = FirstPartyNixPin.Nixpkgs,
                Packages = runtime.NixPackages.Select(package => package.Package).ToArray()
            },
            Provides = ["dotnet"],
            Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["project"] = new() { Required = true, Type = "path" },
                ["sdk"] = new() { Type = "versionRange" }
            },
            Launch = new CapabilityLaunchTemplate
            {
                Command = "dotnet",
                Arguments = ["run", "--project", "{{parameters.project}}"]
            },
            Probe = new CapabilityProbeTemplate { Command = "dotnet", Arguments = ["--version"] }
        };
    }

    public string DefaultNix()
        => FirstPartyNixPin.DefaultNix(new DotnetRuntimeCapability().NixPackages.Select(package => package.Package).ToArray());
}
