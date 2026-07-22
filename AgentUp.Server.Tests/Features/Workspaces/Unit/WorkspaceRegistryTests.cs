using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Models;
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
        _registry = CreateRegistry([new FakeCapabilityAdapter("dotnet"), new FakeCapabilityAdapter("docker")]);
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
                    Ports: [new PortDeclaration("DB_PORT", 5432)],
                    Environment: new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "not-a-real-value" },
                    Volumes: ["pgdata:/var/lib/postgresql/data"])
            ]
        };

        var workspace = await _registry.RegisterAsync(request);
        var db = workspace.Applications[0];

        Assert.That(db.Ports, Has.Count.EqualTo(1));
        Assert.That(db.Ports[0].DefaultPort, Is.EqualTo(5432));
        Assert.That(db.AllocatedPorts, Has.Count.EqualTo(1));
        Assert.That(db.AllocatedPorts[0].AllocatedPort, Is.GreaterThanOrEqualTo(10000));
        Assert.That(db.Environment!["POSTGRES_PASSWORD"], Is.EqualTo("not-a-real-value"));
        Assert.That(db.Volumes, Is.EqualTo(new[] { "pgdata:/var/lib/postgresql/data" }));
    }

    [Test]
    public async Task Register_MixedApplicationsAndServices_AreAllPresent()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Applications = [new ApplicationDefinition("API", "dotnet run", null)],
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

    [Test]
    public async Task Register_TypedDotnet_UsesCapabilityLaunchPlan()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Dotnet =
            [
                new DotnetApplicationDefinition(
                    "Api",
                    "10.0.x",
                    new DotnetRunDefinition("src/Api/Api.csproj", ["--no-launch-profile"]),
                    [new PortDeclaration("API_PORT", 5000)])
            ]
        });

        var app = workspace.Applications.Single();
        Assert.That(app.CapabilityId, Is.EqualTo("dotnet"));
        Assert.That(app.CapabilityVersionRequirement, Is.EqualTo("10.0.x"));
        Assert.That(app.Command, Is.EqualTo("dotnet launch Api"));
        Assert.That(app.CapabilityStatus!.CanRun, Is.True);
        Assert.That(app.AllocatedPorts.Single().Variable, Is.EqualTo("API_PORT"));
    }

    [Test]
    public async Task Register_TypedDotnet_PreservesEnvironmentAndEnvironmentFiles()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Dotnet =
            [
                new DotnetApplicationDefinition(
                    "Api",
                    "10.0.x",
                    new DotnetRunDefinition("src/Api/Api.csproj"),
                    null,
                    new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
                    [".env"])
            ]
        });

        var app = workspace.Applications.Single();
        Assert.That(app.Environment!["ASPNETCORE_ENVIRONMENT"], Is.EqualTo("Development"));
        Assert.That(app.EnvironmentFiles, Is.EqualTo(new[] { ".env" }));
    }

    [Test]
    public async Task Register_TypedDocker_UsesDockerCapabilityMetadata()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Docker =
            [
                new DockerCapabilityDefinition(
                    "Database",
                    "postgres:17",
                    [new PortDeclaration("DB_PORT", 5432)])
            ]
        });

        var app = workspace.Applications.Single();
        Assert.That(app.ServiceType, Is.EqualTo(ServiceType.Docker));
        Assert.That(app.CapabilityId, Is.EqualTo("docker"));
        Assert.That(app.Image, Is.EqualTo("postgres:17"));
        Assert.That(app.CapabilityStatus!.CanRun, Is.True);
    }

    [Test]
    public async Task Register_TypedDocker_PreservesEnvironmentAndEnvironmentFiles()
    {
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Docker =
            [
                new DockerCapabilityDefinition(
                    "Database",
                    "postgres:17",
                    null,
                    new Dictionary<string, string> { ["POSTGRES_USER"] = "user" },
                    ["pgdata:/var/lib/postgresql/data"],
                    [".env.database"])
            ]
        });

        var app = workspace.Applications.Single();
        Assert.That(app.Environment!["POSTGRES_USER"], Is.EqualTo("user"));
        Assert.That(app.EnvironmentFiles, Is.EqualTo(new[] { ".env.database" }));
        Assert.That(app.Volumes, Is.EqualTo(new[] { "pgdata:/var/lib/postgresql/data" }));
    }

    private static WorkspaceRegistry CreateRegistry(IReadOnlyList<ICapabilityAdapter> adapters) =>
        new(
            new InMemoryWorkspaceRepository(),
            new InMemoryPortAllocationService(),
            new CapabilityReconciliationService(adapters));

    private sealed class FakeCapabilityAdapter(string capabilityId) : ICapabilityAdapter
    {
        public CapabilityDescriptor Descriptor { get; } =
            new(capabilityId, capabilityId, "1.0.0", true, ["linux", "macos", "windows"]);

        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(
            [
                new CapabilityInstalledVersion(capabilityId, "1.0.0", capabilityId, CapabilityVersionSource.System, false)
            ]);

        public Task<CapabilityValidationResult> ValidateAsync(
            CapabilityDeclaration declaration,
            IReadOnlyList<CapabilityInstalledVersion> installedVersions,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityValidationResult.Success());

        public Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
            CapabilityDeclaration declaration,
            IReadOnlyList<CapabilityInstalledVersion> installedVersions,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CapabilityLaunchPlan($"{capabilityId} launch {declaration.Name}"));
    }
}
