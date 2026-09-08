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

        var result = await controller.GetChanges(workspaceId) as OkObjectResult;

        var tree = result!.Value as GitChangeTree;
        Assert.That(tree!.FileCount, Is.EqualTo(1));
        Assert.That(tree.Root.Directories[0].Name, Is.EqualTo("src"));
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

        var result = await controller.GetFileDiff(workspaceId, "src/app/main.cs") as OkObjectResult;

        Assert.That((result!.Value as GitFileDiff)!.Diff, Is.EqualTo("@@ -1 +1 @@"));
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
            new GitCommitRequest(["src/app/main.cs"], "feat(App): add main")) as OkObjectResult;

        Assert.That((result!.Value as GitCommitResult)!.Commit, Is.EqualTo("0123456789abcdef"));
        Assert.That(git.CommittedFiles, Is.EqualTo(new[] { "src/app/main.cs" }));
    }

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
