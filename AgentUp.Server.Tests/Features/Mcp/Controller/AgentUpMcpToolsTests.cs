using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Mcp.Controllers;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Mcp.Providers;
using AgentUp.Server.Features.Mcp.Services;
using AgentUp.Server.Features.Mcp.Tools;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Mcp.Controller;

[TestFixture]
public sealed class AgentUpMcpToolsTests
{
    private WorkspaceRegistry _registry = null!;
    private AgentUpMcpTools _tools = null!;
    private FakeConfigurationProvider _configuration = null!;
    private FakeCommitsQueueProvider _commitsQueue = null!;

    [SetUp]
    public async Task SetUp()
    {
        _registry = ServerTestComposition.CreateRegistry();
        await _registry.StartAsync(CancellationToken.None);

        _configuration = new FakeConfigurationProvider();
        var workspaceController = ServerTestComposition.CreateMcpWorkspaceController(
            _registry,
            new NullWorkspaceProcessManager(),
            _configuration,
            new FakeWorkspaceIdentityProvider());
        var contextController = new McpContextController(new McpContextService(new AgentUpContextProvider()));
        _commitsQueue = new FakeCommitsQueueProvider();
        _tools = new AgentUpMcpTools(workspaceController, contextController, CreateCommitsController(_commitsQueue));
    }

    private static McpCommitsController CreateCommitsController(FakeCommitsQueueProvider? queue = null)
    {
        var q = queue ?? new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(q, git);
        var controller = new CommitsController(service);
        return new McpCommitsController(new McpCommitsService(controller));
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
        Assert.That(workspace.RepositoryPath, Is.EqualTo("/repos/inventory"));
        Assert.That(workspace.Branch, Is.EqualTo("main"));
        Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
        Assert.That(workspace.Applications.Single().AllocatedPorts.Single().Variable, Is.EqualTo("WEB_PORT"));
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
    public async Task StartWorkspace_ReturnsStructuredError_WhenConfigurationReadFails()
    {
        var workspaceController = ServerTestComposition.CreateMcpWorkspaceController(
            _registry,
            new NullWorkspaceProcessManager(),
            new ThrowingConfigurationProvider(),
            new FakeWorkspaceIdentityProvider());
        var tools = new AgentUpMcpTools(workspaceController, new McpContextController(new McpContextService(new AgentUpContextProvider())), CreateCommitsController());

        var result = await tools.StartWorkspace("/repos/bad-config", CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("Failed to read agent-up.json"));
        Assert.That(result.Message, Does.Contain("invalid json"));
    }

    [Test]
    public async Task StopWorkspace_StopsById()
    {
        var workspace = await RegisterRunningWorkspace("/repos/app");

        var result = await _tools.StopWorkspace(workspace.Id);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task StopWorkspace_StopsByWorktreePath()
    {
        var workspace = await RegisterRunningWorkspace("/repos/app");

        var result = await _tools.StopWorkspace(worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task StopWorkspace_ReturnsStructuredError_WhenProcessStopFails()
    {
        var workspace = await RegisterRunningWorkspace("/repos/app");
        var workspaceController = ServerTestComposition.CreateMcpWorkspaceController(
            _registry,
            new FailingWorkspaceProcessManager(),
            _configuration,
            new FakeWorkspaceIdentityProvider());
        var tools = new AgentUpMcpTools(workspaceController, new McpContextController(new McpContextService(new AgentUpContextProvider())), CreateCommitsController());

        var result = await tools.StopWorkspace(workspace.Id);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Is.EqualTo("stop failed"));
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Failed));
    }

    [Test]
    public async Task GetWorkspaceStatus_ReturnsSelectedWorkspace()
    {
        var workspace = await RegisterRunningWorkspace("/repos/app");

        var result = _tools.GetWorkspaceStatus(id: workspace.Id);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.SameAs(_registry.GetById(workspace.Id)));
    }

    [Test]
    public async Task GetWorkspaceStatus_ReturnsAllWorkspaces_WhenSelectorIsNotSupplied()
    {
        await RegisterRunningWorkspace("/repos/one");
        await RegisterRunningWorkspace("/repos/two");

        var result = _tools.GetWorkspaceStatus();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.AssignableTo<IReadOnlyList<Workspace>>());
        Assert.That(((IReadOnlyList<Workspace>)result.Data!).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ListWorkspaces_ReturnsRegisteredWorkspaces()
    {
        await RegisterRunningWorkspace("/repos/app");

        var workspaces = _tools.ListWorkspaces();

        Assert.That(workspaces.Single().WorktreePath, Is.EqualTo("/repos/app"));
    }

    [Test]
    public void ContextTools_ReturnCanonicalGuidance()
    {
        Assert.That(_tools.GetAgentUpContext(), Does.Contain("AgentUp.Server is the single source of truth"));
        Assert.That(_tools.GetAgentUpContext(), Does.Contain("deploy my app with Agent-Up"));
        Assert.That(_tools.GetAgentUpContext(), Does.Contain("call start_workspace"));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"services\""));
        Assert.That(_tools.GetAgentUpJsonFormat(), Does.Contain("\"ports\""));
    }

    [Test]
    public void StartWorkspaceDescription_TellsAgentsWhenToUseAgentUp()
    {
        var description = typeof(AgentUpMcpTools)
            .GetMethod(nameof(AgentUpMcpTools.StartWorkspace))!
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
    public async Task EnqueueCommit_ReturnsSuccess_WhenCommitIsEnqueued()
    {
        var result = await _tools.EnqueueCommit(
            "/repos/app",
            "feat/new-thing",
            "feat: add new thing",
            ["src/Thing.cs"],
            null,
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_commitsQueue.Stored!.Commits, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EnqueueCommit_ReturnsFailure_WhenWorktreePathIsEmpty()
    {
        var result = await _tools.EnqueueCommit(
            "",
            "feat/slice",
            "feat: message",
            ["a.cs"],
            null,
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("worktreePath"));
    }

    [Test]
    public async Task EnqueueCommit_ReturnsFailure_WhenNoFilesProvided()
    {
        var result = await _tools.EnqueueCommit(
            "/repos/app",
            "feat/slice",
            "feat: message",
            [],
            null,
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task GetCommitsStatus_ReturnsQueuedEntries()
    {
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["a.cs"], null, CancellationToken.None);

        var result = await _tools.GetCommitsStatus("/repos/app", CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Does.Contain("1 queued entry"));
    }

    [Test]
    public async Task GetCommitsStatus_ReturnsFailure_WhenWorktreePathIsEmpty()
    {
        var result = await _tools.GetCommitsStatus("", CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void EnqueueCommitDescription_TellsAgentsNotToUseGitDirectly()
    {
        var description = typeof(AgentUpMcpTools)
            .GetMethod(nameof(AgentUpMcpTools.EnqueueCommit))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

        Assert.That(description, Does.Contain("Do NOT call git add"));
        Assert.That(description, Does.Contain("git commit"));
        Assert.That(description, Does.Contain("git stash"));
    }

    private async Task<Workspace> RegisterRunningWorkspace(string worktreePath)
    {
        _configuration.Configuration = new AgentUpConfiguration(
            Path.GetFileName(worktreePath),
            [new ApplicationDefinition("App", "dotnet run", "/", [])]);

        var result = await _tools.StartWorkspace(worktreePath, CancellationToken.None);
        Assert.That(result.Succeeded, Is.True);
        return _registry.GetAll().Single(w => w.WorktreePath == worktreePath);
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

    private sealed class ThrowingConfigurationProvider : IAgentUpConfigurationProvider
    {
        public Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("invalid json");
    }

    private sealed class FailingWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
        public Task KillAsync(string workspaceId) => throw new InvalidOperationException("stop failed");
        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
    }

    internal sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;

        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
