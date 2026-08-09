using AgentUp.InstallerConfig;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Factories;

var product = new PackageProductManifest(
    AgentUpProduct.Name,
    AgentUpProduct.Slug,
    AgentUpProduct.EnvironmentPrefix)
{
    Manufacturer = AgentUpProduct.Name,
    WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode,
    WindowsServiceName = AgentUpProduct.Slug + "-server",
    WindowsCliShimName = AgentUpProduct.Slug + ".cmd",
    WindowsServerUrl = "http://127.0.0.1:5000"
};

return await new PackagingServiceRegistry(product).PackageCommands.ExecuteAsync(args);
