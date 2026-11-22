using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Git.Controller;

[TestFixture]
public sealed class GitQueryControllerTests
{
    [Test]
    public async Task GetChangesAsync_returnsNullForAnUnknownWorkspace()
    {
        var controller = await CreateAsync(new FakeGitWorkingTreeProvider());

        var tree = await controller.Query.GetChangesAsync("missing");

        Assert.That(tree, Is.Null);
    }

    [Test]
    public async Task GetHeadAsync_returnsTheWorkspaceBranch()
    {
        var git = new FakeGitWorkingTreeProvider { Branch = "topic" };
        var controller = await CreateAsync(git);

        var head = await controller.Query.GetHeadAsync(controller.WorkspaceId);

        Assert.That(head!.Branch, Is.EqualTo("topic"));
    }

    [Test]
    public async Task GetWorktreeChangesAsync_returnsNullForAnUnknownWorkspace()
    {
        var controller = await CreateAsync(new FakeGitWorkingTreeProvider());

        Assert.That(await controller.Query.GetWorktreeChangesAsync("missing"), Is.Null);
    }

    [Test]
    public async Task GetWorktreeChangesAsync_isEmptyWhenTheFolderIsNotARepository()
    {
        var controller = await CreateAsync(new FakeGitWorkingTreeProvider { Failure = "not a git repository" });

        Assert.That(await controller.Query.GetWorktreeChangesAsync(controller.WorkspaceId), Is.Empty);
    }

    [Test]
    public async Task GetWorktreeChangesAsync_returnsTheWorkspaceChanges()
    {
        var git = new FakeGitWorkingTreeProvider();
        git.Changes.Add(new GitChangeEntry("src/main.cs", GitChangeStatus.Modified));
        var controller = await CreateAsync(git);

        var changes = await controller.Query.GetWorktreeChangesAsync(controller.WorkspaceId);

        Assert.That(changes!.Single().Path, Is.EqualTo("src/main.cs"));
    }

    private static async Task<(GitQueryController Query, string WorkspaceId)> CreateAsync(FakeGitWorkingTreeProvider git)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Named("widgets").At("/clones/widgets").AtCommit("abc123").Build());
        var query = new GitQueryController(new GitChangeTreeService(workspaces, git, new FakeWorkspacePromptGuard()));
        return (query, workspace.Id);
    }
}
