using System.Text.Json;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class CapabilityCatalogParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public CapabilityModuleCatalog ParseCatalog(string json)
    {
        var catalog = JsonSerializer.Deserialize<CapabilityModuleCatalog>(json, Options)
            ?? throw new InvalidOperationException("Capability catalog is empty.");

        if (string.IsNullOrWhiteSpace(catalog.SchemaVersion))
            throw new InvalidOperationException("Capability catalog schemaVersion is required.");

        foreach (var artifact in catalog.Artifacts)
            ValidateArtifact(artifact);

        return catalog;
    }

    private static void ValidateArtifact(CapabilityArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.CapabilityId))
            throw new InvalidOperationException("Capability artifact capabilityId is required.");

        if (string.IsNullOrWhiteSpace(artifact.Version))
            throw new InvalidOperationException($"Capability artifact '{artifact.CapabilityId}' version is required.");

        if (artifact.DownloadUrl is null)
            throw new InvalidOperationException($"Capability artifact '{artifact.CapabilityId}' downloadUrl is required.");

        if (artifact.DownloadUrl.Scheme is not "https")
            throw new InvalidOperationException($"Capability artifact '{artifact.CapabilityId}' must use an HTTPS download URL.");

        if (string.IsNullOrWhiteSpace(artifact.Sha256))
            throw new InvalidOperationException($"Capability artifact '{artifact.CapabilityId}' SHA-256 value is required.");

        if (artifact.Sha256.Length != 64 || artifact.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidOperationException($"Capability artifact '{artifact.CapabilityId}' has an invalid SHA-256 value.");
    }
}
