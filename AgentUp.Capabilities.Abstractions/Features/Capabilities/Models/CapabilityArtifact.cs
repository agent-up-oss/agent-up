namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityArtifact(
    string CapabilityId,
    string Version,
    Uri DownloadUrl,
    string Sha256,
    string? Signature = null);
