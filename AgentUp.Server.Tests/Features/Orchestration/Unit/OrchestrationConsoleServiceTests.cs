using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class OrchestrationConsoleServiceTests
{
    [Test]
    public async Task GetConsoleAsync_redactsStoredLineText()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await ((IHostedService)registry).StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).At(".").Build())
            .Build());
        var output = new InMemoryOutputRepository();
        var processOutput = new ProcessOutputService(output);
        await processOutput.AppendAsync(workspace.Id, ServerDomain.WebName, "token=abc123");
        await processOutput.AppendAsync(
            workspace.Id,
            ServerDomain.WebName,
            "[err] password:secret",
            ProcessOutputStream.Stderr);
        var audit = ServerTestComposition.CreateAuditController(registry);
        var service = new OrchestrationConsoleService(
            new WorkspaceQueryController(registry),
            new ProcessesController(new NullWorkspaceProcessManager(), processOutput),
            audit,
            new ConsoleSecretRedactor(),
            NullLogger<OrchestrationConsoleService>.Instance);

        var result = await service.GetConsoleAsync(workspace.Id, null, 10, 10, CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var snapshot = (WorkspaceConsoleSnapshot)result.Data!;
        Assert.That(
            snapshot.Applications.Single().Lines,
            Is.EqualTo(new[] { "token=[REDACTED]", "[err] password:[REDACTED]" }));
    }
}
