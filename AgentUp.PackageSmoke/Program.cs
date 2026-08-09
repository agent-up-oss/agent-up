using AgentUp.InstallerConfig;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Shared.Factories;

var product = new SmokeProductManifest(
    ServiceName: AgentUpProduct.Slug + "-server",
    CliShimName: AgentUpProduct.Slug,
    ArtifactBaseName: AgentUpProduct.Slug,
    DisplayName: AgentUpProduct.Name,
    InstallDirName: AgentUpProduct.Name,
    WorkspaceConfigFileName: "agent-up.json");

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController(product);
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
