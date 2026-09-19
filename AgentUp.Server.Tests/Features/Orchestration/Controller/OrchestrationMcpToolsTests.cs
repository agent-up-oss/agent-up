using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Shared.Interfaces;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Support;
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
                new NullWorkspaceProcessManager(),
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
                new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand)
                    .At("/")
                    .WithPort(ServerDomain.Port().Named("WEB_PORT").On(5173))
                    .Build()
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
    public async Task StartWorkspace_UsesDisplayOverrides_ForDesktopVisuals()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "Inventory",
            Display: new WorkspaceDisplayConfiguration("Agent 2 - Search", "Search flow"));

        var result = await _tools.StartWorkspace("/repos/inventory", CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var workspace = _registry.GetAll().Single();
        Assert.Multiple(() =>
        {
            Assert.That(workspace.DisplayName, Is.EqualTo("Agent 2 - Search"));
            Assert.That(workspace.Branch, Is.EqualTo("Search flow"));
            Assert.That(workspace.WorktreePath, Is.EqualTo("/repos/inventory"));
            Assert.That(workspace.RepositoryPath, Is.EqualTo("/repos/inventory"));
            Assert.That(workspace.Commit, Is.EqualTo("abc123"));
        });
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

    // The context resource is the one thing every agent reads before it does anything, so
    // each phrase below is a separate promise to agents rather than one lump of prose.
    // Split by the concern the phrase belongs to, so a deleted line names what was lost.
    [TestCase("AgentUp.Server is the single source of truth")]
    [TestCase("deploy my app with Agent-Up")]
    [TestCase("call start_workspace")]
    [TestCase("immediately instead of listing workspaces")]
    public void AgentUpContext_tellsAgentsToStartWorkspacesThroughTheServer(string guidance)
        => Assert.That(_tools.GetAgentUpContext(), Does.Contain(guidance));

    [TestCase("If browser navigation, inspection, waiting, screenshots, or interaction fails or times out")]
    [TestCase("inspect the workspace console immediately")]
    [TestCase("query Audit MCP for recent application console events")]
    public void AgentUpContext_tellsAgentsWhereToLookWhenBrowserWorkFails(string guidance)
        => Assert.That(_tools.GetAgentUpContext(), Does.Contain(guidance));

    [TestCase("Before starting a new coding task")]
    [TestCase("guard_commits")]
    [TestCase("continueWorktreePath")]
    [TestCase("inspect, debug, or continue")]
    [TestCase("enqueue_review_fix_commit")]
    [TestCase("one pull request review issue violation")]
    [TestCase("active merge, rebase, cherry-pick, revert, or bisect")]
    [TestCase("Legacy enqueue restores tracked files")]
    public void AgentUpContext_tellsAgentsHowToWorkTheCommitQueue(string guidance)
        => Assert.That(_tools.GetAgentUpContext(), Does.Contain(guidance));

    [TestCase("Scope every conventional commit message")]
    [TestCase("feat means a user-facing addition")]
    [TestCase("fix means a user-facing fix")]
    [TestCase("test means test-only or smoke-validation changes")]
    [TestCase("chore means maintenance/packaging/CI/tooling")]
    [TestCase("style means CSS/HTML only")]
    [TestCase("docs means documentation only")]
    [TestCase("separate guidance/docs entry")]
    [TestCase("prompts.commitPolicy")]
    public void AgentUpContext_definesEveryConventionalCommitType(string guidance)
        => Assert.That(_tools.GetAgentUpContext(), Does.Contain(guidance));

    [TestCase("\"services\"")]
    [TestCase("\"desktopApplications\"")]
    [TestCase("\"ports\"")]
    [TestCase("\"display\"")]
    [TestCase("\"prompts\"")]
    [TestCase("\"commitPolicy\"")]
    public void AgentUpJsonFormat_documentsEveryTopLevelSection(string section)
        => Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain(section));

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
            [new ApplicationDefinitionBuilder("App", ServerDomain.ApiCommand).At("/").Build()]);
        await _tools.StartWorkspace("/repos/app", CancellationToken.None);
        var workspace = _registry.GetAll().Single();
        var tools = new OrchestrationMcpTools(
            CreateWorkspaceController(
                _registry,
                new FailingWorkspaceProcessManager(),
                _configuration,
                new FakeWorkspaceIdentityProvider()),
            new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider())),
            CreateConsoleController(
                ServerTestComposition.CreateProcessesController(new NullWorkspaceProcessManager()),
                ServerTestComposition.CreateAuditController(_registry)));

        var result = await tools.StopWorkspace(workspace!.Id);

        Assert.That(result.Succeeded, Is.False);
        // WorkspaceLifecycleService.StopAsync returns a stable public message and logs the raw
        // exception server-side, rather than surfacing internal exception text to callers.
        Assert.That(result.Message, Is.EqualTo("Workspace could not be stopped."));
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Failed));
    }

    [Test]
    public async Task GetWorkspaceConsole_ReturnsLiveOutputAndConsoleAuditTrail()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "App",
            [new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.ApiCommand).At("/").Build()]);
        await _tools.StartWorkspace("/repos/app", CancellationToken.None);
        var workspace = _registry.GetAll().Single();

        await _processOutput.AppendAsync(workspace.Id, "Web", "older");
        await _processOutput.AppendAsync(workspace.Id, "Web", "ready");
        await _processOutput.AppendAsync(workspace.Id, "Web", "[err] failed", ProcessOutputStream.Stderr);
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

    [Test]
    public async Task GetWorkspaceConsole_RedactsLiveOutputAndAuditTrailMessages()
    {
        _configuration.Configuration = new AgentUpConfiguration(
            "App",
            [new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.ApiCommand).At("/").Build()]);
        await _tools.StartWorkspace("/repos/app", CancellationToken.None);
        var workspace = _registry.GetAll().Single();

        await _processOutput.AppendAsync(workspace.Id, "Web", "token=abc123");

        var result = await _tools.GetWorkspaceConsole(id: workspace.Id, lineLimit: 10, auditLimit: 10);

        Assert.That(result.Succeeded, Is.True);
        var snapshot = (WorkspaceConsoleSnapshot)result.Data!;
        Assert.That(snapshot.Applications.Single().Lines, Is.EqualTo(new[] { "token=[REDACTED]" }));
        Assert.That(snapshot.AuditTrail.Select(evt => evt.Message), Does.Contain("token=[REDACTED]"));
    }

    private static OrchestrationWorkspaceController CreateWorkspaceController(
        WorkspaceRegistry registry,
        IWorkspaceProcessManager processManager,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
        => ServerTestComposition.CreateOrchestrationWorkspaceController(registry, processManager, configuration, identity);

    private OrchestrationConsoleController CreateConsoleController(
        ProcessesController processes,
        AgentUp.Server.Features.Audit.Controllers.AuditController audit)
        => new(new OrchestrationConsoleService(
            new AgentUp.Server.Features.Workspaces.Controllers.WorkspaceQueryController(_registry),
            processes,
            audit,
            new ConsoleSecretRedactor(),
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
