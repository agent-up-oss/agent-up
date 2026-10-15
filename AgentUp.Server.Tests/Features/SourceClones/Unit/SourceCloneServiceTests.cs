using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.SourceClones.Unit;

[TestFixture]
public sealed class SourceCloneServiceTests
{
    private const string Destination = "/clones/widgets";

    [Test]
    public async Task CloneAsync_clonesAndRegistersWorkspaceForRepositoryWithoutAgentUpJson()
    {
        var git = new FakeSourceCloneGitProvider();
        var service = CreateService(git: git);

        var result = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(git.Cloned!.DestinationPath, Is.EqualTo(Destination));
        Assert.That(result.Workspace!.DisplayName, Is.EqualTo("widgets"));
        Assert.That(result.Workspace.WorktreePath, Is.EqualTo(Destination));
        Assert.That(result.Workspace.Branch, Is.EqualTo("main"));
        Assert.That(result.Workspace.Commit, Is.EqualTo("abc123"));
    }

    [Test]
    public async Task CloneAsync_prefersDeclaredAgentUpConfigurationForRegistration()
    {
        var service = CreateService(configuration: new AgentUpConfiguration("Widgets Store"));

        var result = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Workspace!.DisplayName, Is.EqualTo("Widgets Store"));
    }

    [Test]
    public async Task CloneAsync_returnsValidationErrorWithoutCloning()
    {
        var git = new FakeSourceCloneGitProvider();
        var targets = new FakeSourceCloneTargetProvider { ResolveError = "Branch must be a valid Git branch name." };
        var service = CreateService(targets: targets, git: git);

        var result = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "--upload-pack"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Branch must be a valid Git branch name."));
        Assert.That(git.Cloned, Is.Null);
    }

    [Test]
    public async Task CloneAsync_refusesToOverwriteAnExistingCloneDirectory()
    {
        var git = new FakeSourceCloneGitProvider();
        var targets = new FakeSourceCloneTargetProvider { Exists = true };
        var service = CreateService(targets: targets, git: git);

        var result = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("already exists"));
        Assert.That(git.Cloned, Is.Null);
    }

    [Test]
    public async Task CloneAsync_reportsGitFailuresAsStructuredErrors()
    {
        var git = new FakeSourceCloneGitProvider { CloneError = "Remote branch 'nope' not found." };
        var service = CreateService(git: git);

        var result = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "nope"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Remote branch 'nope' not found."));
        Assert.That(result.Workspace, Is.Null);
    }

    [Test]
    public void GetRoot_exposesTheConfiguredSourceClonesRoot()
    {
        var service = CreateService();

        Assert.That(service.GetRoot().Path, Is.EqualTo("/clones"));
    }

    [Test]
    public async Task CloneAsync_labelsADerivedWorkspaceAsAReviewCopy()
    {
        var git = new FakeSourceCloneGitProvider();
        var targets = new FakeSourceCloneTargetProvider();
        var service = CreateService(targets: targets, git: git);

        var result = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "feature/login")
        {
            Derived = true
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(targets.LastRequest!.Derived, Is.True);
        Assert.That(result.Workspace!.DisplayName, Is.EqualTo("Review · feature/login"));
        Assert.That(result.Workspace.Derived, Is.True);
        Assert.That(result.Workspace.SourceRevision, Is.EqualTo("feature/login"));
        Assert.That(git.Cloned!.Derived, Is.True);
    }

    [Test]
    public async Task CloneAsync_displaysTheRequestedRevisionWhenGitReportsDetachedHead()
    {
        var identity = new FakeWorkspaceIdentityProvider(new WorkspaceIdentity(Destination, "HEAD", "deadbeef"));
        var service = CreateService(identity: identity);

        var result = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "abc1234")
        {
            Derived = true
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Workspace!.Branch, Is.EqualTo("abc1234"));
    }

    [Test]
    public async Task RefreshAsync_fetchesTheNamedRevisionForADerivedWorkspace()
    {
        var git = new FakeSourceCloneGitProvider();
        var service = CreateService(git: git);
        var cloned = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "topic")
        {
            Derived = true
        });

        var refreshed = await service.RefreshAsync(cloned.Workspace!.Id);

        Assert.That(refreshed.Succeeded, Is.True);
        Assert.That(git.Refreshed, Is.EqualTo((Destination, "topic")));
    }

    [Test]
    public async Task RefreshAsync_refusesAWorkspaceThatIsNotDerived()
    {
        var git = new FakeSourceCloneGitProvider();
        var service = CreateService(git: git);
        var cloned = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        var refreshed = await service.RefreshAsync(cloned.Workspace!.Id);

        Assert.That(refreshed.Succeeded, Is.False);
        Assert.That(refreshed.Error, Does.Contain("derived"));
        Assert.That(git.Refreshed, Is.Null);
    }

    [Test]
    public async Task RefreshAsync_refusesADerivedWorkspaceOutsideTheSourceClonesRoot()
    {
        var git = new FakeSourceCloneGitProvider();
        var service = CreateService(git: git, root: new FakeSourceCloneRootProvider("/clones") { UnderRoot = false });
        var cloned = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "topic")
        {
            Derived = true
        });

        var refreshed = await service.RefreshAsync(cloned.Workspace!.Id);

        Assert.That(refreshed.Succeeded, Is.False);
        Assert.That(refreshed.Error, Does.Contain("managed source clone"));
        Assert.That(git.Refreshed, Is.Null);
    }

    [Test]
    public async Task RefreshAsync_reportsGitFailuresAsStructuredErrors()
    {
        var git = new FakeSourceCloneGitProvider { RefreshError = "couldn't find remote ref" };
        var service = CreateService(git: git);
        var cloned = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "topic")
        {
            Derived = true
        });

        var refreshed = await service.RefreshAsync(cloned.Workspace!.Id);

        Assert.That(refreshed.Succeeded, Is.False);
        Assert.That(refreshed.Error, Is.EqualTo("couldn't find remote ref"));
        Assert.That(git.Refreshed, Is.EqualTo((Destination, "topic")));
    }

    [Test]
    public async Task RemovingADerivedWorkspaceDeletesTheManagedCloneDirectory()
    {
        var root = new FakeSourceCloneRootProvider("/clones");
        var registry = ServerTestComposition.CreateRegistry();
        var service = CreateService(root: root, registry: registry);
        var cloned = await service.CloneAsync(new CloneSourceRequest(
            "https://example.test/acme/widgets.git",
            "topic")
        {
            Derived = true
        });

        await registry.RemoveAsync(cloned.Workspace!.Id);

        Assert.That(root.DeletedPaths, Is.EqualTo(new[] { Destination }));
    }

    [Test]
    public async Task RemovingANonDerivedWorkspaceLeavesTheWorkingTree()
    {
        var root = new FakeSourceCloneRootProvider("/clones");
        var registry = ServerTestComposition.CreateRegistry();
        var service = CreateService(root: root, registry: registry);
        var cloned = await service.CloneAsync(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        await registry.RemoveAsync(cloned.Workspace!.Id);

        Assert.That(root.DeletedPaths, Is.Empty);
    }

    private static SourceCloneService CreateService(
        FakeSourceCloneTargetProvider? targets = null,
        FakeSourceCloneGitProvider? git = null,
        AgentUpConfiguration? configuration = null,
        FakeWorkspaceIdentityProvider? identity = null,
        FakeSourceCloneRootProvider? root = null,
        AgentUp.Server.Features.Workspaces.Services.WorkspaceRegistry? registry = null)
    {
        identity ??= new FakeWorkspaceIdentityProvider(new WorkspaceIdentity(Destination, "main", "abc123"));
        return new(
            targets ?? new FakeSourceCloneTargetProvider(),
            git ?? new FakeSourceCloneGitProvider(),
            root ?? new FakeSourceCloneRootProvider("/clones"),
            identity,
            new OrchestrationRegistrationController(new OrchestrationRegistrationService(
                new FakeAgentUpConfigurationProvider(configuration),
                identity)),
            new WorkspaceQueryController(registry ?? ServerTestComposition.CreateRegistry()));
    }
}
