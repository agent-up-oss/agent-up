using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Tests.Support;

internal static class AgentUpPackageTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";
    private const string EnvironmentPrefix = "AGENTUP";
    private const string WindowsUpgradeCode = "8f9076cc-95f1-4bd6-a087-1686e5e0d540";

    private static readonly PackageProductManifest ProductManifest = CreateProduct();

    public static PackageProductManifest Product()
        => ProductManifest;

    private static PackageProductManifest CreateProduct()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            Manufacturer = ProductName,
            WindowsUpgradeCode = WindowsUpgradeCode,
            InstallerApplication = new PackageProductArtifact("agent-up-installer", "Installer", "", "AgentUp.InstallerApp", "AgentUp.InstallerApp/AgentUp.InstallerApp.csproj", "installer", LocalInstallerArtifactTarget.InstallerApp),
            InstallerOptions =
            [
                new PackageProductArtifact("agent-up-cli", "CLI", "", "AgentUp.CLI", "AgentUp.CLI/AgentUp.CLI.csproj", "cli", LocalInstallerArtifactTarget.Cli),
                new PackageProductArtifact("agent-up-server", "Server", "", "AgentUp.Server", "AgentUp.Server/AgentUp.Server.csproj", "server", LocalInstallerArtifactTarget.Server),
                new PackageProductArtifact("agent-up-desktop", "Desktop", "", "AgentUp.Desktop", "AgentUp.Desktop/AgentUp.Desktop.csproj", "desktop", LocalInstallerArtifactTarget.Desktop),
                new PackageProductArtifact("agent-up-tray", "Tray", "", "AgentUp.Tray", "AgentUp.Tray/AgentUp.Tray.csproj", "tray", LocalInstallerArtifactTarget.Tray)
            ]
        };

    public static ProductManifest InstallerProduct()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            WindowsUpgradeCode = WindowsUpgradeCode
        };
}
