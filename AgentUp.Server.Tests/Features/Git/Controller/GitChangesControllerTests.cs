using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Git.Controller;

[TestFixture]
public sealed class GitChangesControllerTests
{
    [Test]
    public async Task GetChanges_returnsNotFoundForAnUnknownWorkspace()
    {
        var (controller, _) = await CreateControllerAsync(new FakeGitWorkingTreeProvider());

        var result = await controller.GetChanges("missing");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetChanges_returnsTheDirectoryTreeForAKnownWorkspace()
    {
        var git = new FakeGitWorkingTreeProvider();
        git.Changes.Add(new GitChangeEntry("src/app/main.cs", GitChangeStatus.Modified));
        var (controller, workspaceId) = await CreateControllerAsync(git);

        var result = await controller.GetChanges(workspaceId);

        var tree = OkValue<GitChangeTree>(result);
        Assert.That(tree.FileCount, Is.EqualTo(1));
        Assert.That(tree.Root.Directories[0].Name, Is.EqualTo("src"));
    }

    [Test]
    public async Task GetHead_returnsNotFoundForAnUnknownWorkspace()
    {
        var (controller, _) = await CreateControllerAsync(new FakeGitWorkingTreeProvider());

        var result = await controller.GetHead("missing");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetHead_returnsTheLiveBranch()
    {
        var git = new FakeGitWorkingTreeProvider { Branch = "topic" };
        git.LocalBranches.Add("topic");
        var (controller, workspaceId) = await CreateControllerAsync(git);

        var result = await controller.GetHead(workspaceId);

        Assert.That(OkValue<GitHeadState>(result).Branch, Is.EqualTo("topic"));
    }

    [Test]
    public async Task GetFileDiff_returnsNotFoundForAFileWithoutChanges()
    {
        var (controller, workspaceId) = await CreateControllerAsync(new FakeGitWorkingTreeProvider());

        var result = await controller.GetFileDiff(workspaceId, "src/app/main.cs");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetFileDiff_returnsTheDiffForAChangedFile()
    {
        var git = new FakeGitWorkingTreeProvider
        {
            FileDiff = new GitFileDiff("src/app/main.cs", GitChangeStatus.Modified, false, "@@ -1 +1 @@")
        };
        var (controller, workspaceId) = await CreateControllerAsync(git);

        var result = await controller.GetFileDiff(workspaceId, "src/app/main.cs");

        Assert.That(OkValue<GitFileDiff>(result).Diff, Is.EqualTo("@@ -1 +1 @@"));
    }

    [Test]
    public async Task Commit_returnsNotFoundForAnUnknownWorkspace()
    {
        var (controller, _) = await CreateControllerAsync(new FakeGitWorkingTreeProvider());

        var result = await controller.Commit("missing", new GitCommitRequest(["a.cs"], "chore: touch"));

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Commit_returnsTheCommitForSelectedFiles()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (controller, workspaceId) = await CreateControllerAsync(git);

        var result = await controller.Commit(
            workspaceId,
            new GitCommitRequest(["src/app/main.cs"], "feat(App): add main"));

        Assert.That(OkValue<GitCommitResult>(result).Commit, Is.EqualTo("0123456789abcdef"));
        Assert.That(git.CommittedFiles, Is.EqualTo(new[] { "src/app/main.cs" }));
    }

    [Test]
    public async Task Discard_returnsTheMutationResult()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (controller, workspaceId) = await CreateControllerAsync(git);

        var result = await controller.Discard(workspaceId, new GitFilesRequest(["src/app/main.cs"]));

        Assert.That(OkValue<GitMutationResult>(result).Succeeded, Is.True);
        Assert.That(git.DiscardedFiles, Is.EqualTo(new[] { "src/app/main.cs" }));
    }

    [Test]
    public async Task SwitchBranch_returnsNotFoundForAnUnknownWorkspace()
    {
        var (controller, _) = await CreateControllerAsync(new FakeGitWorkingTreeProvider());

        var result = await controller.SwitchBranch("missing", new GitBranchRequest("topic", false));

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    private static T OkValue<T>(IActionResult result) where T : class
        => (result as OkObjectResult)?.Value as T
           ?? throw new AssertionException($"Expected a 200 OK result carrying {typeof(T).Name}, got {result.GetType().Name}.");

    private static async Task<(GitChangesController Controller, string WorkspaceId)> CreateControllerAsync(
        FakeGitWorkingTreeProvider git)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(new RegisterWorkspaceRequest(
            DisplayName: "widgets",
            RepositoryPath: "/clones/widgets",
            WorktreePath: "/clones/widgets",
            Branch: "main",
            Commit: "abc123"));

        var controller = new GitChangesController(new GitChangeTreeService(workspaces, git))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, workspace.Id);
    }
}
