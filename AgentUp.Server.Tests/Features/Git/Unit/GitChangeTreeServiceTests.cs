using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Git.Unit;

[TestFixture]
public sealed class GitChangeTreeServiceTests
{
    private const string WorktreePath = "/clones/widgets";

    [Test]
    public async Task GetChangesAsync_returnsNullForAnUnknownWorkspace()
    {
        var service = new GitChangeTreeService(
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new FakeGitWorkingTreeProvider());

        Assert.That(await service.GetChangesAsync("missing"), Is.Null);
    }

    [Test]
    public async Task GetChangesAsync_groupsChangedFilesByDirectory()
    {
        var git = new FakeGitWorkingTreeProvider();
        git.Changes.AddRange([
            new GitChangeEntry("README.md", GitChangeStatus.Modified),
            new GitChangeEntry("src/app/main.cs", GitChangeStatus.Added),
            new GitChangeEntry("src/app/util.cs", GitChangeStatus.Modified),
            new GitChangeEntry("src/tests/main.tests.cs", GitChangeStatus.Untracked)
        ]);
        var (service, workspaceId) = await CreateServiceAsync(git);

        var tree = await service.GetChangesAsync(workspaceId);

        Assert.That(tree!.FileCount, Is.EqualTo(4));
        Assert.That(tree.Root.Files.Select(file => file.Name), Is.EqualTo(new[] { "README.md" }));
        Assert.That(tree.Root.Directories.Select(directory => directory.Name), Is.EqualTo(new[] { "src" }));

        var source = tree.Root.Directories[0];
        Assert.That(source.Path, Is.EqualTo("src"));
        Assert.That(source.Files, Is.Empty);
        Assert.That(source.Directories.Select(directory => directory.Path), Is.EqualTo(new[] { "src/app", "src/tests" }));
        Assert.That(source.Directories[0].Files.Select(file => file.Path),
            Is.EqualTo(new[] { "src/app/main.cs", "src/app/util.cs" }));
        Assert.That(source.Directories[0].Files[0].Status, Is.EqualTo(GitChangeStatus.Added));
    }

    [Test]
    public async Task GetChangesAsync_reportsAnEmptyTreeWhenTheWorktreeIsNotAGitRepository()
    {
        var git = new FakeGitWorkingTreeProvider { Failure = "Git worktree path must be an existing local directory." };
        var (service, workspaceId) = await CreateServiceAsync(git);

        var tree = await service.GetChangesAsync(workspaceId);

        Assert.That(tree!.FileCount, Is.Zero);
        Assert.That(tree.Root.Files, Is.Empty);
        Assert.That(tree.Root.Directories, Is.Empty);
    }

    [Test]
    public async Task GetChangesAsync_readsTheWorkspaceWorktreePath()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (service, workspaceId) = await CreateServiceAsync(git);

        await service.GetChangesAsync(workspaceId);

        Assert.That(git.LastWorktreePath, Is.EqualTo(WorktreePath));
    }

    [Test]
    public async Task GetHeadAsync_returnsTheLiveBranch()
    {
        var git = new FakeGitWorkingTreeProvider { Branch = "topic" };
        git.LocalBranches.Add("topic");
        var (service, workspaceId) = await CreateServiceAsync(git);

        var head = await service.GetHeadAsync(workspaceId);

        Assert.That(head!.Branch, Is.EqualTo("topic"));
        Assert.That(head.LocalBranches, Does.Contain("topic"));
    }

    [Test]
    public async Task GetHeadAsync_returnsNullForAnUnknownWorkspace()
    {
        var service = new GitChangeTreeService(
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new FakeGitWorkingTreeProvider());

        Assert.That(await service.GetHeadAsync("missing"), Is.Null);
    }

    [Test]
    public async Task GetFileDiffAsync_returnsNullForAFileWithoutChanges()
    {
        var (service, workspaceId) = await CreateServiceAsync(new FakeGitWorkingTreeProvider());

        Assert.That(await service.GetFileDiffAsync(workspaceId, "src/app/main.cs"), Is.Null);
    }

    [Test]
    public async Task GetFileDiffAsync_returnsNullWhenTheProviderRejectsThePath()
    {
        var git = new FakeGitWorkingTreeProvider { Failure = "Git file path '../escape' must stay under the repository root." };
        var (service, workspaceId) = await CreateServiceAsync(git);

        Assert.That(await service.GetFileDiffAsync(workspaceId, "../escape"), Is.Null);
    }

    [Test]
    public async Task GetFileDiffAsync_returnsTheProviderDiff()
    {
        var git = new FakeGitWorkingTreeProvider
        {
            FileDiff = new GitFileDiff("src/app/main.cs", GitChangeStatus.Modified, false, "@@ -1 +1 @@")
        };
        var (service, workspaceId) = await CreateServiceAsync(git);

        var diff = await service.GetFileDiffAsync(workspaceId, "src/app/main.cs");

        Assert.That(diff!.Diff, Is.EqualTo("@@ -1 +1 @@"));
        Assert.That(diff.IsBinary, Is.False);
    }

    [Test]
    public async Task CommitAsync_commitsOnlyTheSelectedFiles()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (service, workspaceId) = await CreateServiceAsync(git);

        var result = await service.CommitAsync(
            workspaceId,
            new GitCommitRequest(["src/app/main.cs"], "feat(App): add main"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Commit, Is.EqualTo("0123456789abcdef"));
        Assert.That(git.CommittedFiles, Is.EqualTo(new[] { "src/app/main.cs" }));
        Assert.That(git.CommittedMessage, Is.EqualTo("feat(App): add main"));
    }

    [Test]
    public async Task CommitAsync_reportsProviderFailuresWithoutThrowing()
    {
        var git = new FakeGitWorkingTreeProvider { Failure = "Commit message is required." };
        var (service, workspaceId) = await CreateServiceAsync(git);

        var result = await service.CommitAsync(workspaceId, new GitCommitRequest(["src/app/main.cs"], " "));

        Assert.That(result.Found, Is.True);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Commit message is required."));
    }

    [Test]
    public async Task DiscardAsync_discardsOnlyTheSelectedFiles()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (service, workspaceId) = await CreateServiceAsync(git);

        var result = await service.DiscardAsync(workspaceId, new GitFilesRequest(["src/app/main.cs"]));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(git.DiscardedFiles, Is.EqualTo(new[] { "src/app/main.cs" }));
    }

    [Test]
    public async Task SwitchBranchAsync_createsTheRequestedBranch()
    {
        var git = new FakeGitWorkingTreeProvider();
        var (service, workspaceId) = await CreateServiceAsync(git);

        var result = await service.SwitchBranchAsync(workspaceId, new GitBranchRequest("topic", true));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(git.SwitchedBranch, Is.EqualTo("topic"));
        Assert.That(git.CreatedBranch, Is.True);
    }

    [Test]
    public async Task DiscardAsync_reportsNotFoundForAnUnknownWorkspace()
    {
        var service = new GitChangeTreeService(
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new FakeGitWorkingTreeProvider());

        var result = await service.DiscardAsync("missing", new GitFilesRequest(["a.cs"]));

        Assert.That(result.Found, Is.False);
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task DiscardAsync_reportsGitFailures()
    {
        var git = new FakeGitWorkingTreeProvider { Failure = "not a git repository" };
        var (service, workspaceId) = await CreateServiceAsync(git);

        var result = await service.DiscardAsync(workspaceId, new GitFilesRequest(["a.cs"]));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("not a git repository"));
    }

    [Test]
    public async Task SwitchBranchAsync_reportsNotFoundForAnUnknownWorkspace()
    {
        var service = new GitChangeTreeService(
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new FakeGitWorkingTreeProvider());

        var result = await service.SwitchBranchAsync("missing", new GitBranchRequest("topic", false));

        Assert.That(result.Found, Is.False);
    }

    [Test]
    public async Task CommitAsync_reportsNotFoundForAnUnknownWorkspace()
    {
        var service = new GitChangeTreeService(
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new FakeGitWorkingTreeProvider());

        var result = await service.CommitAsync("missing", new GitCommitRequest(["a.cs"], "chore: touch"));

        Assert.That(result.Found, Is.False);
        Assert.That(result.Succeeded, Is.False);
    }

    private static async Task<(GitChangeTreeService Service, string WorkspaceId)> CreateServiceAsync(
        FakeGitWorkingTreeProvider git)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(new RegisterWorkspaceRequest(
            DisplayName: "widgets",
            RepositoryPath: WorktreePath,
            WorktreePath: WorktreePath,
            Branch: "main",
            Commit: "abc123"));

        return (new GitChangeTreeService(workspaces, git), workspace.Id);
    }
}
