using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Factories;
using LocalInstaller.Sample;

var product = new PackageProductManifest(
    SampleProduct.Name,
    SampleProduct.Slug,
    SampleProduct.EnvironmentPrefix)
{
    Manufacturer = SampleProduct.Name,
    WindowsUpgradeCode = SampleProduct.UpgradeCode,
    WindowsServiceName = SampleProduct.Slug + "-server",
    WindowsCliShimName = SampleProduct.Slug + ".cmd",
    WindowsServerUrl = SampleProduct.ServerUrl
};

return await new PackagingServiceRegistry(product).PackageCommands.ExecuteAsync(args);
