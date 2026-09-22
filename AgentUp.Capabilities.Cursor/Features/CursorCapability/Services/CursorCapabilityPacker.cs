using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

public sealed class CursorCapabilityPacker
{
    public const string PackageId = CursorAgentCapability.PackageId;
    public const string PackageVersion = CursorAgentCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
        => new()
        {
            SchemaVersion = "1",
            Id = PackageId,
            Version = PackageVersion,
            DisplayName = "Cursor",
            Publisher = "agent-up",
            Kind = "agent",
            Module = "AgentUp.Capabilities.Cursor.dll",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = FirstPartyNixPin.Nixpkgs,
                Packages = ["nodejs_22"]
            },
            Provides = ["agent"],
            Launch = new CapabilityLaunchTemplate { Command = "agent", Arguments = ["acp"] },
            Probe = new CapabilityProbeTemplate { Command = "agent", Arguments = ["--version"] }
        };

    public string DefaultNix() => FirstPartyNixPin.DefaultNix(["nodejs_22"]);
}
