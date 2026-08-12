using LocalInstaller.Core.Composition;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.App.Features.Installation.Factories;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create(ProductManifest product, string? fakeInstallerVariable = null, string? nixOsLookupOnlyVariable = null)
        => InstallerPlatformAdapterFactory.Create(
            product,
            AppContext.BaseDirectory,
            fakeInstallerVariable is null ? null : Environment.GetEnvironmentVariable(fakeInstallerVariable),
            UseNixOsLookupOnlyMode(nixOsLookupOnlyVariable));

    public static bool UseNixOsLookupOnlyMode(string? nixOsLookupOnlyVariable = null)
        => nixOsLookupOnlyVariable is not null
           && Environment.GetEnvironmentVariable(nixOsLookupOnlyVariable) == "1"
           || InstallerPlatformAdapterFactory.IsNixOsHost();
}
