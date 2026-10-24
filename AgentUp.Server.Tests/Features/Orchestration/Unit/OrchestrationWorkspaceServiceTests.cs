using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class OrchestrationWorkspaceServiceTests
{
    [Test]
    public async Task StartAsync_namesFailedApplicationsWhenTheWorkspaceIsDegraded()
    {
        var service = await CreateService(new PartialFailWorkspaceProcessManager());
        var workspaceId = service.List().Single().Id;

        var result = await service.StartAsync(workspaceId, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Message, Does.Contain("Failed:"));
            Assert.That(result.Message, Does.Contain(ServerDomain.ApiName));
        });
    }

    [Test]
    public async Task StartAsync_omitsFailedNamesWhenEveryApplicationRuns()
    {
        var service = await CreateService(new NullWorkspaceProcessManager());
        var workspaceId = service.List().Single().Id;

        var result = await service.StartAsync(workspaceId, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Message, Does.Not.Contain("Failed:"));
        });
    }

    private static async Task<OrchestrationWorkspaceService> CreateService(IWorkspaceProcessManager processes)
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, ServerDomain.ApiCommand).Build())
            .Build());
        return new OrchestrationWorkspaceService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            new WorkspaceLifecycleController(
                ServerTestComposition.CreateWorkspaceLifecycleService(registry, processes)),
            new OrchestrationRegistrationService(
                new FakeAgentUpConfigurationProvider(),
                new FakeWorkspaceIdentityProvider(new WorkspaceIdentity(
                    ServerDomain.RepositoryPath,
                    ServerDomain.Branch,
                    ServerDomain.Commit))),
            ServerTestComposition.CreateWorkspaceTargetController(registry));
    }
}
