using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Shared.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Orchestration.Controller;

[TestFixture]
public sealed class OrchestrationMcpToolsTests
{
    private WorkspaceRegistry _registry = null!;
    private OrchestrationMcpTools _tools = null!;
    private FakeConfigurationProvider _configuration = null!;
    private InMemoryOutputRepository _output = null!;
    private InMemoryAuditEventRepository _auditEvents = null!;
    private ProcessOutputService _processOutput = null!;

    [SetUp]
    public async Task SetUp()
    {
        _registry = ServerTestComposition.CreateRegistry();
        await _registry.StartAsync(CancellationToken.None);

        _configuration = new FakeConfigurationProvider();
        _output = new InMemoryOutputRepository();
        _auditEvents = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(_registry, _auditEvents);
        _processOutput = new ProcessOutputService(
            _output,
            audit,
            NullLogger<ProcessOutputService>.Instance);
        var processes = new ProcessesController(
            new NullWorkspaceProcessManager(),
            _processOutput);
        _tools = new OrchestrationMcpTools(
            CreateWorkspaceController(
                _registry,
                processes,
                _configuration,
                new FakeWorkspaceIdentityProvider()),
            new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider())),
            CreateConsoleController(processes, audit));
    }

    [Test]
    public async Task StartWorkspace_RegistersAndStarts_FromConfiguration()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "Inventory",
            [
                new ApplicationDefinition(
                    "Frontend",
                    "npm run dev",
                    "/",
                    [new PortDeclaration("WEB_PORT", 5173)])
            ]);

        var result = await _tools.StartWorkspace("/repos/inventory", CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var workspace = _registry.GetAll().Single();
        Assert.That(workspace.DisplayName, Is.EqualTo("Inventory"));
        Assert.That(workspace.WorktreePath, Is.EqualTo("/repos/inventory"));
        Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task StartWorkspace_ReturnsGuidance_WhenAgentUpJsonIsMissing()
    {
        _configuration.Configuration = null;

        var result = await _tools.StartWorkspace("/repos/missing-config", CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("agent-up.json was not found"));
        Assert.That(result.Message, Does.Contain("docs/user-docs/agent-up-json.md"));
        Assert.That(result.Message, Does.Contain("ask the user"));
        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public void ContextTools_ReturnCanonicalGuidance()
    {
        var context = _tools.GetAgentUpContext();

        Assert.That(context, Does.Contain("AgentUp.Server is the single source of truth"));
        Assert.That(context, Does.Contain("deploy my app with Agent-Up"));
        Assert.That(context, Does.Contain("call start_workspace"));
        Assert.That(context, Does.Contain("immediately instead of listing workspaces"));
        Assert.That(context, Does.Contain("If browser navigation, inspection, waiting, screenshots, or interaction fails or times out"));
        Assert.That(context, Does.Contain("inspect the workspace console immediately"));
        Assert.That(context, Does.Contain("query Audit MCP for recent application console events"));
        Assert.That(context, Does.Contain("Before starting a new coding task"));
        Assert.That(context, Does.Contain("guard_commits"));
        Assert.That(context, Does.Contain("inspect, debug, or continue"));
        Assert.That(context, Does.Contain("enqueue_review_fix_commit"));
        Assert.That(context, Does.Contain("one pull request review issue violation"));
        Assert.That(context, Does.Contain("Scope every conventional commit message"));
        Assert.That(context, Does.Contain("feat means a user-facing addition"));
        Assert.That(context, Does.Contain("fix means a user-facing fix"));
        Assert.That(context, Does.Contain("test means test-only or smoke-validation changes"));
        Assert.That(context, Does.Contain("chore means maintenance/packaging/CI/tooling"));
        Assert.That(context, Does.Contain("prompts.commitPolicy"));
        Assert.That(context, Does.Contain("style means CSS/HTML only"));
        Assert.That(context, Does.Contain("docs means documentation only"));
        Assert.That(context, Does.Contain("separate guidance/docs entry"));
        Assert.That(context, Does.Contain("active merge, rebase, cherry-pick, revert, or bisect"));
        Assert.That(context, Does.Contain("enqueue_commit intentionally restores tracked files"));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"services\""));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"ports\""));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"prompts\""));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"commitPolicy\""));
    }

    [Test]
    public void StartWorkspaceDescription_TellsAgentsWhenToUseAgentUp()
    {
        var description = typeof(OrchestrationMcpTools)
            .GetMethod(nameof(OrchestrationMcpTools.StartWorkspace))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

        Assert.That(description, Does.Contain("deploy"));
        Assert.That(description, Does.Contain("run"));
        Assert.That(description, Does.Contain("Agent-Up"));
        Assert.That(description, Does.Contain("local development environment"));
        Assert.That(description, Does.Contain("Do not list workspaces or check status first"));
    }

    [Test]
    public void WorkspaceDiscoveryDescriptions_AvoidPreStartDiscovery()
    {
        var statusDescription = ToolDescription(nameof(OrchestrationMcpTools.GetWorkspaceStatus));
        var listDescription = ToolDescription(nameof(OrchestrationMcpTools.ListWorkspaces));

        Assert.That(statusDescription, Does.Contain("Do not call before start_workspace"));
        Assert.That(listDescription, Does.Contain("Do not call before start_workspace"));
    }

    [Test]
    public void ConsoleDescription_TellsAgentsToUseConsoleAfterBrowserFailure()
    {
        var description = ToolDescription(nameof(OrchestrationMcpTools.GetWorkspaceConsole));

        Assert.That(description, Does.Contain("Call this first"));
        Assert.That(description, Does.Contain("browser navigation"));
        Assert.That(description, Does.Contain("fails or times out"));
    }

    [Test]
    public void ServerInstructions_TellAgentsToStartThenCheckConsoleOnBrowserFailure()
    {
        Assert.That(AgentUpMcpGuidance.ServerInstructions, Does.Contain("call start_workspace with its absolute path immediately"));
        Assert.That(AgentUpMcpGuidance.ServerInstructions, Does.Contain("Do not call list_workspaces or get_workspace_status before start_workspace"));
        Assert.That(AgentUpMcpGuidance.ServerInstructions, Does.Contain("If browser navigation, inspection, waiting, screenshots, or interaction fails or times out"));
        Assert.That(AgentUpMcpGuidance.ServerInstructions, Does.Contain("inspect the workspace console immediately"));
    }

    [Test]
    public async Task StopWorkspace_ReturnsStructuredError_WhenProcessStopFails()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "App",
            [new ApplicationDefinition("App", "dotnet run", "/", [])]);
        await _tools.StartWorkspace("/repos/app", CancellationToken.None);
        var workspace = _registry.GetAll().Single();
        var tools = new OrchestrationMcpTools(
            CreateWorkspaceController(
                _registry,
                ServerTestComposition.CreateProcessesController(new FailingWorkspaceProcessManager()),
                _configuration,
                new FakeWorkspaceIdentityProvider()),
            new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider())),
            CreateConsoleController(
                ServerTestComposition.CreateProcessesController(new NullWorkspaceProcessManager()),
                ServerTestComposition.CreateAuditController(_registry)));

        var result = await tools.StopWorkspace(workspace!.Id);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Is.EqualTo("stop failed"));
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Failed));
    }

    [Test]
    public async Task GetWorkspaceConsole_ReturnsLiveOutputAndConsoleAuditTrail()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "App",
            [new ApplicationDefinition("Web", "dotnet run", "/", [])]);
        await _tools.StartWorkspace("/repos/app", CancellationToken.None);
        var workspace = _registry.GetAll().Single();

        await _processOutput.AppendAsync(workspace.Id, "Web", "older");
        await _processOutput.AppendAsync(workspace.Id, "Web", "ready");
        await _processOutput.AppendAsync(workspace.Id, "Web", "[err] failed");
        await _tools.GetWorkspaceConsole(id: workspace.Id, lineLimit: 2, auditLimit: 10);

        var result = await _tools.GetWorkspaceConsole(id: workspace.Id, lineLimit: 2, auditLimit: 10);

        Assert.That(result.Succeeded, Is.True);
        var snapshot = (WorkspaceConsoleSnapshot)result.Data!;
        var app = snapshot.Applications.Single();
        Assert.That(app.ApplicationName, Is.EqualTo("Web"));
        Assert.That(app.TotalLineCount, Is.EqualTo(3));
        Assert.That(app.Truncated, Is.True);
        Assert.That(app.Lines, Is.EqualTo(new[] { "ready", "[err] failed" }));
        Assert.That(snapshot.AuditTrail.Select(evt => evt.Message), Does.Contain("ready"));
        Assert.That(snapshot.AuditTrail.Select(evt => evt.Stream), Does.Contain("stderr"));
        Assert.That(_auditEvents.Events, Has.Some.Matches<AgentUp.Server.Features.Audit.Models.AuditEvent>(
            evt => evt.Action == "workspace_console_snapshot"));
    }

    private static OrchestrationWorkspaceController CreateWorkspaceController(
        WorkspaceRegistry registry,
        ProcessesController processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
        => new(new OrchestrationWorkspaceService(
            new AgentUp.Server.Features.Workspaces.Controllers.WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            processes,
            configuration,
            identity));

    private OrchestrationConsoleController CreateConsoleController(
        ProcessesController processes,
        AgentUp.Server.Features.Audit.Controllers.AuditController audit)
        => new(new OrchestrationConsoleService(
            new AgentUp.Server.Features.Workspaces.Controllers.WorkspaceQueryController(_registry),
            processes,
            audit,
            NullLogger<OrchestrationConsoleService>.Instance));

    private static string ToolDescription(string methodName)
        => typeof(OrchestrationMcpTools)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

    private sealed class FakeConfigurationProvider : IAgentUpConfigurationProvider
    {
        public AgentUpConfiguration? Configuration { get; set; }

        public Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken) =>
            Task.FromResult(Configuration);
    }

    private sealed class FakeWorkspaceIdentityProvider : IWorkspaceIdentityProvider
    {
        public Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkspaceIdentity(worktreePath, "main", "abc123"));
    }

    private sealed class FailingWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
        public Task KillAsync(string workspaceId) => throw new InvalidOperationException("stop failed");
        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
    }
}
