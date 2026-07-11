using AgentUp.Server.Features.Workspaces;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceRegistryTests
{
    private WorkspaceRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new WorkspaceRegistry();
    }

    [Test]
    public void GetAll_ReturnsEmpty_Initially()
    {
        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public void Register_ReturnsWorkspace_WithAllSuppliedFields()
    {
        var request = new RegisterWorkspaceRequest(
            DisplayName: "Agent 1",
            RepositoryPath: "/repos/app",
            WorktreePath: "/repos/app/.worktrees/agent-1",
            Branch: "feature/auth",
            Commit: "abc1234");

        var workspace = _registry.Register(request);

        Assert.That(workspace.DisplayName, Is.EqualTo("Agent 1"));
        Assert.That(workspace.RepositoryPath, Is.EqualTo("/repos/app"));
        Assert.That(workspace.WorktreePath, Is.EqualTo("/repos/app/.worktrees/agent-1"));
        Assert.That(workspace.Branch, Is.EqualTo("feature/auth"));
        Assert.That(workspace.Commit, Is.EqualTo("abc1234"));
    }

    [Test]
    public void Register_AssignsNonEmptyId()
    {
        var workspace = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        Assert.That(workspace.Id, Is.Not.Empty);
    }

    [Test]
    public void Register_AssignsUniqueIds()
    {
        var a = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var b = _registry.Register(new RegisterWorkspaceRequest("B", "/r", "/r/b", "main", "c2"));

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));
    }

    [Test]
    public void Register_DefaultsStateTo_Stopped()
    {
        var workspace = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public void GetAll_ReturnsAllRegisteredWorkspaces()
    {
        _registry.Register(new RegisterWorkspaceRequest("Alpha", "/r", "/r/a", "main", "c1"));
        _registry.Register(new RegisterWorkspaceRequest("Beta", "/r", "/r/b", "main", "c2"));

        Assert.That(_registry.GetAll(), Has.Count.EqualTo(2));
    }

    [Test]
    public void GetById_ReturnsWorkspace_WhenExists()
    {
        var registered = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var found = _registry.GetById(registered.Id);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(registered.Id));
    }

    [Test]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        var result = _registry.GetById("does-not-exist");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void UpdateState_ChangesState_WhenWorkspaceExists()
    {
        var workspace = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var updated = _registry.UpdateState(workspace.Id, WorkspaceState.Running);

        Assert.That(updated, Is.True);
        Assert.That(_registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public void UpdateState_ReturnsFalse_WhenWorkspaceNotFound()
    {
        var result = _registry.UpdateState("ghost", WorkspaceState.Running);

        Assert.That(result, Is.False);
    }

    [Test]
    public void UpdateState_DoesNotAffect_OtherWorkspaces()
    {
        var a = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));
        var b = _registry.Register(new RegisterWorkspaceRequest("B", "/r", "/r/b", "main", "c2"));

        _registry.UpdateState(a.Id, WorkspaceState.Running);

        Assert.That(_registry.GetById(b.Id)!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public void Remove_RemovesWorkspace_WhenExists()
    {
        var workspace = _registry.Register(new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"));

        var removed = _registry.Remove(workspace.Id);

        Assert.That(removed, Is.True);
        Assert.That(_registry.GetById(workspace.Id), Is.Null);
        Assert.That(_registry.GetAll(), Is.Empty);
    }

    [Test]
    public void Remove_ReturnsFalse_WhenNotFound()
    {
        var result = _registry.Remove("ghost");

        Assert.That(result, Is.False);
    }

    [Test]
    public void MultipleWorkspaces_HaveIsolated_Fields()
    {
        var a = _registry.Register(new RegisterWorkspaceRequest(
            "Alpha", "/repos/app", "/repos/app/.worktrees/alpha", "feature/alpha", "aaa0001"));
        var b = _registry.Register(new RegisterWorkspaceRequest(
            "Beta", "/repos/app", "/repos/app/.worktrees/beta", "feature/beta", "bbb0002"));

        Assert.That(_registry.GetById(a.Id)!.WorktreePath, Is.Not.EqualTo(_registry.GetById(b.Id)!.WorktreePath));
        Assert.That(_registry.GetById(a.Id)!.Branch, Is.Not.EqualTo(_registry.GetById(b.Id)!.Branch));
        Assert.That(_registry.GetById(a.Id)!.Commit, Is.Not.EqualTo(_registry.GetById(b.Id)!.Commit));
    }
}
