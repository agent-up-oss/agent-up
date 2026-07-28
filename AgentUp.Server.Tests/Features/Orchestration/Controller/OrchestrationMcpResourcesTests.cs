using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Orchestration.Controller;

[TestFixture]
public sealed class OrchestrationMcpResourcesTests
{
    private WorkspaceRegistry _registry = null!;
    private OrchestrationMcpResources _resources = null!;

    [SetUp]
    public async Task SetUp()
    {
        _registry = ServerTestComposition.CreateRegistry();
        await _registry.StartAsync(CancellationToken.None);

        var workspaceController = ServerTestComposition.CreateOrchestrationWorkspaceController(
            _registry,
            new NullWorkspaceProcessManager(),
            new FakeConfigurationProvider(),
            new FakeWorkspaceIdentityProvider());
        var contextController = new OrchestrationContextController(new OrchestrationContextService(new AgentUpContextProvider()));
        _resources = new OrchestrationMcpResources(workspaceController, contextController);
    }

    [Test]
    public void ContextResource_ReturnsAgentUpRules()
    {
        var context = _resources.GetAgentUpContext();

        Assert.That(context, Does.Contain("AgentUp.Server is the single source of truth"));
        Assert.That(context, Does.Contain("registered Agent-Up MCP tools"));
    }

    [Test]
    public void ContextResourceDescription_PointsAgentsToRegisteredTools()
    {
        var description = typeof(OrchestrationMcpResources)
            .GetMethod(nameof(OrchestrationMcpResources.GetAgentUpContext))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

        Assert.That(description, Does.Contain("registered Agent-Up MCP tools"));
    }

    [Test]
    public void AgentUpJsonResource_ReturnsCurrentFormat()
    {
        var format = _resources.GetAgentUpJsonFormat();

        Assert.That(format, Does.Contain("\"applications\""));
        Assert.That(format, Does.Contain("\"services\""));
        Assert.That(format, Does.Contain("\"ports\""));
    }

    [Test]
    public async Task WorkspacesResource_ReturnsRegisteredWorkspaces()
    {
        await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "abc"));

        var json = _resources.ListWorkspaces();

        Assert.That(json, Does.Contain("\"displayName\": \"A\""));
        Assert.That(json, Does.Contain("\"worktreePath\": \"/r/a\""));
    }

    [Test]
    public async Task WorkspaceResource_ReturnsRegisteredWorkspace()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "abc"));

        var json = _resources.GetWorkspace(workspace.Id);

        Assert.That(json, Does.Contain($"\"id\": \"{workspace.Id}\""));
        Assert.That(json, Does.Contain("\"displayName\": \"A\""));
    }

    [Test]
    public void WorkspaceResource_ReturnsNotFoundPayload_ForUnknownWorkspace()
    {
        var json = _resources.GetWorkspace("missing");

        Assert.That(json, Does.Contain("Workspace not found."));
    }

    private sealed class FakeConfigurationProvider : IAgentUpConfigurationProvider
    {
        public Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken) =>
            Task.FromResult<AgentUpConfiguration?>(null);
    }

    private sealed class FakeWorkspaceIdentityProvider : IWorkspaceIdentityProvider
    {
        public Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkspaceIdentity(worktreePath, "main", "abc123"));
    }
}
