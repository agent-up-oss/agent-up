namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityPackageManifest
{
    public string SchemaVersion { get; init; } = "1";
    public string Id { get; init; } = "";
    public string Version { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Module { get; init; } = "";
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public CapabilityNixSpec? Nix { get; init; }
    public IReadOnlyList<string> Provides { get; init; } = [];
    public IReadOnlyDictionary<string, CapabilityParameterSpec> Parameters { get; init; } =
        new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase);
    public CapabilityLaunchTemplate? Launch { get; init; }
    public CapabilityProbeTemplate? Probe { get; init; }
}
