using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Registry.Features.LocalStore.DTOs;

public sealed record LocalRegistryPackageDto(
    CapabilityPackageManifest Manifest,
    string PackageDirectory,
    bool HasDefaultNix);
