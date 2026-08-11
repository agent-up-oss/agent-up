using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Smoke.Features.InstallerFlowValidation.DTOs;

public sealed record InstallerFlowProductManifest(
    string ProductName,
    string Slug,
    string EnvironmentPrefix)
{
    internal ProductManifest ToManifest()
        => new(ProductName, Slug, EnvironmentPrefix)
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
        };
}
