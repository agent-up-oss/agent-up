using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

public sealed class ClaudeCapabilityPacker
{
    public const string PackageId = ClaudeAgentCapability.PackageId;
    public const string PackageVersion = ClaudeAgentCapability.PackageVersion;

    public CapabilityPackageManifest Manifest()
        => new()
        {
            SchemaVersion = "1",
            Id = PackageId,
            Version = PackageVersion,
            DisplayName = "Claude",
            Publisher = "agent-up",
            Kind = "agent",
            Module = "AgentUp.Capabilities.Claude.dll",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = FirstPartyNixPin.Nixpkgs,
                Packages = ["nodejs_22"]
            },
            Provides = ["claude-agent-acp"],
            Launch = new CapabilityLaunchTemplate { Command = "claude-agent-acp", Arguments = [] },
            Probe = new CapabilityProbeTemplate { Command = "claude-agent-acp", Arguments = ["--version"] }
        };

    public string DefaultNix() => FirstPartyNixPin.DefaultNix(["nodejs_22"]);
}
