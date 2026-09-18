using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Diagnostics.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Diagnostics.Unit;

[TestFixture]
public sealed class WorkspaceDiagnosticsServiceTests
{
    [Test]
    public async Task GetAsync_AggregatesOnlyOwningWorkspaceAndIdentifiesContexts()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Shop")
            .At("/repo")
            .AtCommit("abc")
            .WithApplication(new ApplicationDefinitionBuilder("web", "npm start").At(".").Build())
            .Build());
        workspace.State = WorkspaceState.Running;
        workspace.Applications.Single().State = ApplicationState.Unhealthy;
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(registry, events);
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("frontend")
            .From("web")
            .Doing("javascript_exception")
            .Outcome("failure")
            .ForWorkspace(workspace.Id)
            .WithDetails(new Dictionary<string, string> { ["application"] = "web", ["browserSessionId"] = "browser-7", ["message"] = "boom" })
            .Build(), CancellationToken.None);
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("frontend")
            .From("web")
            .Doing("network_request_failed")
            .Outcome("resolved")
            .ForWorkspace(workspace.Id)
            .WithDetails(new Dictionary<string, string> { ["application"] = "web", ["url"] = "/api/cart" })
            .Build(), CancellationToken.None);
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("frontend")
            .From("web")
            .Doing("javascript_exception")
            .Outcome("failure")
            .ForWorkspace("another-workspace")
            .WithDetails(new Dictionary<string, string> { ["application"] = "web" })
            .Build(), CancellationToken.None);
        var output = new InMemoryOutputRepository();
        var processOutput = new ProcessOutputService(output, audit, NullLogger<ProcessOutputService>.Instance);
        await processOutput.AppendAsync(workspace.Id, "web", "ready");
        await processOutput.AppendAsync(workspace.Id, "web", "[err] failed", ProcessOutputStream.Stderr);
        var health = new AppHealthController(new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            audit,
            NullLogger<AppHealthCheckService>.Instance));
        var service = new WorkspaceDiagnosticsService(
            new WorkspaceQueryController(registry),
            new ProcessesController(new NullWorkspaceProcessManager(), processOutput),
            health,
            audit,
            new ConsoleSecretRedactor());

        var result = await service.GetAsync(workspace.Id, null, 10, 10, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.WorkspaceId, Is.EqualTo(workspace.Id));
            Assert.That(result.ProcessState, Is.EqualTo("Running"));
            Assert.That(result.Applications.Single().ProcessState, Is.EqualTo("Unhealthy"));
            Assert.That(result.Applications.Single().Logs, Is.EqualTo(new[] { "ready", "[err] failed" }));
            Assert.That(result.Entries, Has.Count.EqualTo(3));
            Assert.That(result.Entries, Has.Some.Matches<AgentUp.Server.Features.Diagnostics.DTOs.DiagnosticEntryDto>(
                entry => entry.Category == "javascript" && entry.State == "active" && entry.Application == "web" && entry.BrowserSession == "browser-7"));
            Assert.That(result.Entries, Has.Some.Matches<AgentUp.Server.Features.Diagnostics.DTOs.DiagnosticEntryDto>(
                entry => entry.Category == "network" && entry.State == "resolved"));
        });
    }

    [Test]
    public async Task GetAsync_ReturnsNullForUnknownWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var audit = ServerTestComposition.CreateAuditController(registry);
        var health = new AppHealthController(new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry), audit,
            NullLogger<AppHealthCheckService>.Instance));
        var service = new WorkspaceDiagnosticsService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateProcessesController(new NullWorkspaceProcessManager()),
            health,
            audit,
            new ConsoleSecretRedactor());

        Assert.That(await service.GetAsync("missing", null, 10, 10, CancellationToken.None), Is.Null);
    }
}
