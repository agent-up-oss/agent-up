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

    private static SourceCloneService CreateService(
        FakeSourceCloneTargetProvider? targets = null,
        FakeSourceCloneGitProvider? git = null,
        AgentUpConfiguration? configuration = null)
        => new(
            targets ?? new FakeSourceCloneTargetProvider(),
            git ?? new FakeSourceCloneGitProvider(),
            new FakeSourceCloneRootProvider("/clones"),
            new FakeWorkspaceIdentityProvider(new WorkspaceIdentity(Destination, "main", "abc123")),
            new OrchestrationRegistrationController(new OrchestrationRegistrationService(
                new FakeAgentUpConfigurationProvider(configuration),
                new FakeWorkspaceIdentityProvider(new WorkspaceIdentity(Destination, "main", "abc123")))),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));
}
