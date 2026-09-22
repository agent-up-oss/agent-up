namespace AgentUp.Capabilities.Common.Features.NixRuntime.Models;

public sealed record NixEnvironmentSpec(
    string? DefaultNixPath,
    string? NixpkgsRev,
    IReadOnlyList<string> Packages);
