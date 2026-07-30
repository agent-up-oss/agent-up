using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Composition;

public static partial class InstallerPlatformAdapterFactory
{
    public const string FakeInstallerVariable = AgentUpInstallerEnvironment.FakeInstallerVariable;
    public const string PayloadRootVariable = AgentUpInstallerEnvironment.PayloadRootVariable;
    public const string NixOsLookupOnlyVariable = AgentUpInstallerEnvironment.NixOsLookupOnlyVariable;

    public static IInstallerPlatformAdapter Create()
        => Create(
            ProductManifest.AgentUp(),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(FakeInstallerVariable),
            UseNixOsLookupOnlyMode());

    public static string ResolvePayloadRoot(string appBaseDirectory)
        => ResolvePayloadRoot(appBaseDirectory, ProductManifest.AgentUp());

    public static bool UseNixOsLookupOnlyMode()
        => Environment.GetEnvironmentVariable(NixOsLookupOnlyVariable) == "1" || IsNixOsHost();
}
