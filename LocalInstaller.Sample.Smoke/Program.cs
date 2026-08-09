using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Shared.Factories;
using LocalInstaller.Sample;

var product = new SmokeProductManifest(
    ServiceName: SampleProduct.Slug + "-server",
    CliShimName: SampleProduct.Slug,
    ArtifactBaseName: SampleProduct.Slug,
    DisplayName: SampleProduct.Name,
    InstallDirName: SampleProduct.Name,
    WorkspaceConfigFileName: SampleProduct.WorkspaceConfigFileName);

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController(product);
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
