using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Tests.Features.Orchestration.Controller;

[TestFixture]
public sealed class OrchestrationMcpToolsTests
{
    private WorkspaceRegistry _registry = null!;
    private OrchestrationMcpTools _tools = null!;
    private FakeConfigurationProvider _configuration = null!;

    [SetUp]
    public async Task SetUp()
    {
        _registry = ServerTestComposition.CreateRegistry();
        await _registry.StartAsync(CancellationToken.None);

        _configuration = new FakeConfigurationProvider();
        _tools = new OrchestrationMcpTools(
            ServerTestComposition.CreateOrchestrationWorkspaceController(
                _registry,
                new NullWorkspaceProcessManager(),
                _configuration,
                new FakeWorkspaceIdentityProvider()),
            new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider())));
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
        Assert.That(context, Does.Contain("Before starting a new coding task"));
        Assert.That(context, Does.Contain("guard_commits"));
        Assert.That(context, Does.Contain("inspect, debug, or continue"));
        Assert.That(context, Does.Contain("enqueue_review_fix_commit"));
        Assert.That(context, Does.Contain("one pull request review issue violation"));
        Assert.That(context, Does.Contain("feat means a user-facing addition"));
        Assert.That(context, Does.Contain("fix means a user-facing fix"));
        Assert.That(context, Does.Contain("style means CSS/HTML only"));
        Assert.That(context, Does.Contain("docs means documentation only"));
        Assert.That(context, Does.Contain("separate guidance/docs entry"));
        Assert.That(context, Does.Contain("active merge, rebase, cherry-pick, revert, or bisect"));
        Assert.That(context, Does.Contain("enqueue_commit intentionally restores tracked files"));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"services\""));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"ports\""));
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
        Assert.That(description, Does.Contain("local development environments"));
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
            ServerTestComposition.CreateOrchestrationWorkspaceController(
                _registry,
                new FailingWorkspaceProcessManager(),
                _configuration,
                new FakeWorkspaceIdentityProvider()),
            new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider())));

        var result = await tools.StopWorkspace(workspace!.Id);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Is.EqualTo("stop failed"));
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Failed));
    }

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
