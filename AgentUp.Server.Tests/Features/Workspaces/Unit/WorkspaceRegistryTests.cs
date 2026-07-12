using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceRegistryTests
{
    private WorkspaceRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new WorkspaceRegistry(new InMemoryWorkspaceRepository());
    }

    [Test]
    public void GetAll_ReturnsEmpty_Initially()
    {
        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public async Task Register_ReturnsWorkspace_WithAllSuppliedFields()
    {
        var request = new RegisterWorkspaceRequest(
            DisplayName: "Agent 1",
            RepositoryPath: "/repos/app",
            WorktreePath: "/repos/app/.worktrees/agent-1",
            Branch: "feature/auth",
            Commit: "abc1234");

        var workspace = await _registry.RegisterAsync(request);

        Assert.That(workspace.DisplayName, Is.EqualTo("Agent 1"));
        Assert.That(workspace.RepositoryPath, Is.EqualTo("/repos/app"));
        Assert.That(workspace.WorktreePath, Is.EqualTo("/repos/app/.worktrees/agent-1"));
        Assert.That(workspace.Branch, Is.EqualTo("feature/auth"));
        Assert.That(workspace.Commit, Is.EqualTo("abc1234"));
    }

    [Test]
    public async Task Register_AssignsNonEmptyId()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        Assert.That(workspace.Id, Is.Not.Empty);
    }

    [Test]
    public async Task Register_AssignsUniqueIds()
    {
        var a = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var b = await _registry.RegisterAsync(new RegisterWorkspaceRequest("B", "/r", "/r/b", "main", "c2"));

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));
    }

    [Test]
    public async Task Register_SameWorktreePath_RetainsId_AndDoesNotDuplicate()
    {
        var first = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var second = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c2"));

        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(_registry.GetAll(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Register_SameWorktreePath_UpdatesFields()
    {
        await _registry.RegisterAsync(new RegisterWorkspaceRequest("Old Name", "/r", "/r/a", "main", "c1"));
        var updated = await _registry.RegisterAsync(new RegisterWorkspaceRequest("New Name", "/r", "/r/a", "feature", "c2"));

        Assert.That(updated.DisplayName, Is.EqualTo("New Name"));
        Assert.That(updated.Branch, Is.EqualTo("feature"));
        Assert.That(updated.Commit, Is.EqualTo("c2"));
    }

    [Test]
    public async Task Register_SameWorktreePath_ResetsStateTo_Stopped()
    {
        var first = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        await _registry.UpdateStateAsync(first.Id, WorkspaceState.Running);

        var second = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c2"));

        Assert.That(second.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task Register_DefaultsStateTo_Stopped()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task GetAll_ReturnsAllRegisteredWorkspaces()
    {
        await _registry.RegisterAsync(new RegisterWorkspaceRequest("Alpha", "/r", "/r/a", "main", "c1"));
        await _registry.RegisterAsync(new RegisterWorkspaceRequest("Beta", "/r", "/r/b", "main", "c2"));

        Assert.That(_registry.GetAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetById_ReturnsWorkspace_WhenExists()
    {
        var registered = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var found = _registry.GetById(registered.Id);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(registered.Id));
    }

    [Test]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        Assert.That(_registry.GetById("does-not-exist"), Is.Null);
    }

    [Test]
    public async Task UpdateState_ChangesState_WhenWorkspaceExists()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var updated = await _registry.UpdateStateAsync(workspace.Id, WorkspaceState.Running);

        Assert.That(updated, Is.True);
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public async Task UpdateState_ReturnsFalse_WhenWorkspaceNotFound()
    {
        var result = await _registry.UpdateStateAsync("ghost", WorkspaceState.Running);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateState_DoesNotAffect_OtherWorkspaces()
    {
        var a = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var b = await _registry.RegisterAsync(new RegisterWorkspaceRequest("B", "/r", "/r/b", "main", "c2"));

        await _registry.UpdateStateAsync(a.Id, WorkspaceState.Running);

        Assert.That(_registry.GetById(b.Id)!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task Remove_RemovesWorkspace_WhenExists()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var removed = await _registry.RemoveAsync(workspace.Id);

        Assert.That(removed, Is.True);
        Assert.That(_registry.GetById(workspace.Id), Is.Null);
        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public async Task Remove_ReturnsFalse_WhenNotFound()
    {
        var result = await _registry.RemoveAsync("ghost");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task MultipleWorkspaces_HaveIsolated_Fields()
    {
        var a = await _registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Alpha", "/repos/app", "/repos/app/.worktrees/alpha", "feature/alpha", "aaa0001"));
        var b = await _registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Beta", "/repos/app", "/repos/app/.worktrees/beta", "feature/beta", "bbb0002"));

        Assert.That(_registry.GetById(a.Id)!.WorktreePath, Is.Not.EqualTo(_registry.GetById(b.Id)!.WorktreePath));
        Assert.That(_registry.GetById(a.Id)!.Branch, Is.Not.EqualTo(_registry.GetById(b.Id)!.Branch));
        Assert.That(_registry.GetById(a.Id)!.Commit, Is.Not.EqualTo(_registry.GetById(b.Id)!.Commit));
    }

    [Test]
    public async Task Register_DockerServices_AreIncluded_InApplicationsList()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Services = [new DockerServiceDefinition("Database", "postgres:16")]
        };

        var workspace = await _registry.RegisterAsync(request);

        Assert.That(workspace.Applications, Has.Count.EqualTo(1));
        Assert.That(workspace.Applications[0].Name, Is.EqualTo("Database"));
        Assert.That(workspace.Applications[0].ServiceType, Is.EqualTo(ServiceType.Docker));
    }

    [Test]
    public async Task Register_DockerService_PreservesImage()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Services = [new DockerServiceDefinition("Cache", "redis:7")]
        };

        var workspace = await _registry.RegisterAsync(request);

        Assert.That(workspace.Applications[0].Image, Is.EqualTo("redis:7"));
    }

    [Test]
    public async Task Register_DockerService_PreservesPorts_Environment_Volumes()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Services =
            [
                new DockerServiceDefinition(
                    Name: "Database",
                    Image: "postgres:16",
                    Ports: ["5432:5432"],
                    Environment: new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "secret" },
                    Volumes: ["pgdata:/var/lib/postgresql/data"])
            ]
        };

        var workspace = await _registry.RegisterAsync(request);
        var db = workspace.Applications[0];

        Assert.That(db.Ports, Is.EqualTo(new[] { "5432:5432" }));
        Assert.That(db.Environment!["POSTGRES_PASSWORD"], Is.EqualTo("secret"));
        Assert.That(db.Volumes, Is.EqualTo(new[] { "pgdata:/var/lib/postgresql/data" }));
    }

    [Test]
    public async Task Register_MixedApplicationsAndServices_AreAllPresent()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Applications = [new ApplicationDefinition("API", "dotnet run", null, null)],
            Services = [new DockerServiceDefinition("Database", "postgres:16")]
        };

        var workspace = await _registry.RegisterAsync(request);

        Assert.That(workspace.Applications, Has.Count.EqualTo(2));
        Assert.That(workspace.Applications.Single(a => a.Name == "API").ServiceType, Is.EqualTo(ServiceType.Process));
        Assert.That(workspace.Applications.Single(a => a.Name == "Database").ServiceType, Is.EqualTo(ServiceType.Docker));
    }

    [Test]
    public async Task Register_DockerService_DefaultsStateTo_Stopped()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Services = [new DockerServiceDefinition("Database", "postgres:16")]
        };

        var workspace = await _registry.RegisterAsync(request);

        Assert.That(workspace.Applications[0].State, Is.EqualTo(ApplicationState.Stopped));
    }
}
