using System.Text.Json.Serialization;

namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityNixpkgsPin
{
    /// <summary>
    /// The nixpkgs commit this package resolves its Nix packages against. This is the pin: a
    /// commit already names its own content.
    /// </summary>
    public string Rev { get; init; } = "";

    /// <summary>
    /// An optional SRI hash of that nixpkgs tree, for a publisher that wants to record one.
    /// </summary>
    /// <remarks>
    /// Nothing in Agent-Up fetches by it: a packed <c>default.nix</c> goes through
    /// <c>&lt;nixpkgs&gt;</c>, and the flake path builds <c>github:NixOS/nixpkgs/{rev}</c>, which
    /// git already content-addresses. It was once required, which only meant every first-party
    /// package shipped the same placeholder.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha256 { get; init; }
}
