using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

public sealed class CodexCapabilityPacker
{
    public const string PackageId = CodexAgentCapability.PackageId;
    public const string PackageVersion = CodexAgentCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
        => new()
        {
            SchemaVersion = "1",
            Id = PackageId,
            Version = PackageVersion,
            DisplayName = "Codex",
            Publisher = "agent-up",
            Kind = "agent",
            Module = "AgentUp.Capabilities.Codex.dll",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = FirstPartyNixPin.Nixpkgs,
                Packages = ["nodejs_22"]
            },
            Provides = ["codex-acp", "codex"],
            Launch = new CapabilityLaunchTemplate { Command = "codex-acp", Arguments = [] },
            Probe = new CapabilityProbeTemplate { Command = "codex-acp", Arguments = ["--version"] }
        };

    public string DefaultNix() => FirstPartyNixPin.DefaultNix(["nodejs_22"]);
}
