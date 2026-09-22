using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.Packages.DTOs;
using AgentUp.Sdk.Common;

namespace AgentUp.Registry.Features.Packages.Services;

public sealed class CapabilityPackageValidator
{
    public const string SchemaVersion = "1";

    public CapabilityPackageValidationResult Validate(CapabilityPackageManifest? manifest)
    {
        if (manifest is null)
            return CapabilityPackageValidationResult.Failure("Capability package manifest is required.");

        var messages = new List<string>();
        if (!string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            messages.Add("Capability package schemaVersion must be '1'.");
        if (string.IsNullOrWhiteSpace(manifest.Id))
            messages.Add("Capability package id is required.");
        if (string.IsNullOrWhiteSpace(manifest.Version))
            messages.Add("Capability package version is required.");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
            messages.Add("Capability package displayName is required.");
        if (string.IsNullOrWhiteSpace(manifest.Publisher))
            messages.Add("Capability package publisher is required.");
        if (string.IsNullOrWhiteSpace(manifest.Kind))
            messages.Add("Capability package kind is required.");
        else if (!CapabilityKind.Allowed.Contains(manifest.Kind, StringComparer.Ordinal))
            messages.Add($"Capability package kind '{manifest.Kind}' is not in the allowlist.");

        if (manifest.Nix is not null)
            ValidateNix(manifest.Nix, messages);

        return messages.Count == 0
            ? CapabilityPackageValidationResult.Success()
            : CapabilityPackageValidationResult.Failure([.. messages]);
    }

    private static void ValidateNix(CapabilityNixSpec nix, List<string> messages)
    {
        if (nix.Nixpkgs is { } pin)
        {
            if (string.IsNullOrWhiteSpace(pin.Rev))
                messages.Add("Capability package nix.nixpkgs.rev is required when nixpkgs is set.");
            // Optional, because nothing resolves packages by it. Checked only for shape, so a
            // publisher that records one cannot record something that is not a hash at all.
            if (pin.Sha256 is { } sha256 && !sha256.StartsWith("sha256-", StringComparison.Ordinal))
                messages.Add("Capability package nix.nixpkgs.sha256 must be an SRI hash.");
        }

        messages.AddRange(nix.Packages
            .Where(string.IsNullOrWhiteSpace)
            .Select(_ => "Capability package nix.packages must not contain empty names."));
    }
}
