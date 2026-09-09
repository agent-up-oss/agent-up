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
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Shared.Providers;
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
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Shop", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("web", "npm start", ".", [])]
        });
        workspace.State = WorkspaceState.Running;
        workspace.Applications.Single().State = ApplicationState.Unhealthy;
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(registry, events);
        await audit.RecordAsync(new AuditRecordRequest("frontend", "web", "javascript_exception", "failure", workspace.Id,
            new Dictionary<string, string> { ["application"] = "web", ["browserSessionId"] = "browser-7", ["message"] = "boom" }), CancellationToken.None);
        await audit.RecordAsync(new AuditRecordRequest("frontend", "web", "network_request_failed", "resolved", workspace.Id,
            new Dictionary<string, string> { ["application"] = "web", ["url"] = "/api/cart" }), CancellationToken.None);
        await audit.RecordAsync(new AuditRecordRequest("frontend", "web", "javascript_exception", "failure", "another-workspace",
            new Dictionary<string, string> { ["application"] = "web" }), CancellationToken.None);
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
