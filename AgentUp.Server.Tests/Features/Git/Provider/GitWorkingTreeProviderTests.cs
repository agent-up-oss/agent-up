using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitWorkingTreeProviderTests
{
    private string _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _repository = Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-worktree-{Guid.NewGuid():N}");
        await TestGitRepository.InitializeAsync(_repository);
        Directory.CreateDirectory(Path.Join(_repository, "src", "app"));
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "app", "main.cs"), "// main\n");
        await TestGitRepository.CommitAllAsync(_repository, "initial");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repository))
            Directory.Delete(_repository, recursive: true);
    }

    [Test]
    public async Task GetChangesAsync_reportsModifiedUntrackedAndDeletedFiles()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "app", "util.cs"), "// util\n");
        File.Delete(Path.Join(_repository, "src", "app", "main.cs"));
        var provider = new GitWorkingTreeProvider();

        var changes = await provider.GetChangesAsync(_repository);

        Assert.That(changes.Select(change => change.Path),
            Is.EqualTo(new[] { "README.md", "src/app/main.cs", "src/app/util.cs" }));
        Assert.That(changes[0].Status, Is.EqualTo(GitChangeStatus.Modified));
        Assert.That(changes[1].Status, Is.EqualTo(GitChangeStatus.Deleted));
        Assert.That(changes[2].Status, Is.EqualTo(GitChangeStatus.Untracked));
    }

    [Test]
    public async Task GetChangesAsync_reportsRenameDestinationsOnce()
    {
        await TestGitRepository.RunAsync(_repository, "mv", "README.md", "READ.md");
        var provider = new GitWorkingTreeProvider();

        var changes = await provider.GetChangesAsync(_repository);

        Assert.That(changes.Select(change => change.Path), Is.EqualTo(new[] { "READ.md" }));
        Assert.That(changes[0].Status, Is.EqualTo(GitChangeStatus.Renamed));
    }

    [Test]
    public async Task GetChangesAsync_listsUntrackedFilesIndividuallyRatherThanTheirDirectory()
    {
        Directory.CreateDirectory(Path.Join(_repository, "docs", "guide"));
        await File.WriteAllTextAsync(Path.Join(_repository, "docs", "guide", "a.md"), "a\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "docs", "guide", "b.md"), "b\n");
        var provider = new GitWorkingTreeProvider();

        var changes = await provider.GetChangesAsync(_repository);

        Assert.That(changes.Select(change => change.Path),
            Is.EqualTo(new[] { "docs/guide/a.md", "docs/guide/b.md" }));
    }

    [Test]
    public async Task GetFileDiffAsync_returnsATrackedFileDiff()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        var provider = new GitWorkingTreeProvider();

        var diff = await provider.GetFileDiffAsync(_repository, "README.md");

        Assert.That(diff!.Status, Is.EqualTo(GitChangeStatus.Modified));
        Assert.That(diff.IsBinary, Is.False);
        Assert.That(diff.Diff, Does.Contain("+widgets changed"));
    }

    [Test]
    public async Task GetFileDiffAsync_returnsAnUntrackedFileDiff()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "NOTES.md"), "notes\n");
        var provider = new GitWorkingTreeProvider();

        var diff = await provider.GetFileDiffAsync(_repository, "NOTES.md");

        Assert.That(diff!.Status, Is.EqualTo(GitChangeStatus.Untracked));
        Assert.That(diff.Diff, Does.Contain("+notes"));
    }

    [Test]
    public async Task GetFileDiffAsync_returnsNullForAnUnchangedFile()
    {
        var provider = new GitWorkingTreeProvider();

        Assert.That(await provider.GetFileDiffAsync(_repository, "README.md"), Is.Null);
    }

    [TestCase("../outside.txt")]
    [TestCase(":(glob)**/*.cs")]
    [TestCase("--output=/tmp/pwned")]
    public void GetFileDiffAsync_rejectsUnsafePaths(string path)
    {
        var provider = new GitWorkingTreeProvider();

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetFileDiffAsync(_repository, path));
    }

    [Test]
    public async Task CommitAsync_commitsOnlySelectedFilesAndLeavesTheRestUnstaged()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "app", "util.cs"), "// util\n");
        var provider = new GitWorkingTreeProvider();

        var commit = await provider.CommitAsync(_repository, ["src/app/util.cs"], "feat(App): add util");

        Assert.That(commit, Has.Length.EqualTo(40));
        Assert.That(await TestGitRepository.ReadAsync(_repository, "log", "-1", "--pretty=%s"),
            Is.EqualTo("feat(App): add util"));
        Assert.That(await TestGitRepository.ReadAsync(_repository, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD"),
            Is.EqualTo("src/app/util.cs"));

        var remaining = await provider.GetChangesAsync(_repository);
        Assert.That(remaining.Select(change => change.Path), Is.EqualTo(new[] { "README.md" }));
    }

    [Test]
    public async Task CommitAsync_commitsDeletions()
    {
        File.Delete(Path.Join(_repository, "src", "app", "main.cs"));
        var provider = new GitWorkingTreeProvider();

        await provider.CommitAsync(_repository, ["src/app/main.cs"], "fix(App): drop main");

        Assert.That(await provider.GetChangesAsync(_repository), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CommitAsync_rejectsEmptyCommitMessages(string message)
    {
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(_repository, ["README.md"], message));

        Assert.That(exception!.Message, Does.Contain("Commit message"));
    }

    [TestCase(".")]
    [TestCase("./")]
    [TestCase("src")]
    [TestCase("src/app")]
    public async Task CommitAsync_rejectsPathsThatAreNotASingleChangedFile(string path)
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "app", "main.cs"), "// changed\n");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(_repository, [path], "chore: sweep"));

        Assert.That(exception!.Message, Does.Contain("not a changed file"));
        Assert.That(await provider.GetChangesAsync(_repository), Has.Count.EqualTo(2),
            "a rejected selection must leave every change in the worktree");
    }

    [Test]
    public async Task CommitAsync_rejectsAFileWithoutChanges()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(_repository, ["src/app/main.cs"], "chore: untouched"));

        Assert.That(exception!.Message, Does.Contain("not a changed file"));
    }

    // A backslash is a legal character in a Unix file name, so the path git reports must survive
    // unchanged into the diff and commit calls. Windows cannot create such a file at all.
    [Test]
    [Platform("Unix")]
    public async Task ChangedPathsWithBackslashesRoundTripThroughDiffAndCommit()
    {
        var name = "odd\\name.cs";
        await File.WriteAllTextAsync(Path.Join(_repository, name), "// odd\n");
        var provider = new GitWorkingTreeProvider();

        var changes = await provider.GetChangesAsync(_repository);
        var reported = changes.Single(change => change.Path.Contains("odd", StringComparison.Ordinal)).Path;

        Assert.That(reported, Is.EqualTo(name));
        Assert.That(await provider.GetFileDiffAsync(_repository, reported), Is.Not.Null);

        await provider.CommitAsync(_repository, [reported], "feat(App): add odd name");

        Assert.That(await provider.GetChangesAsync(_repository), Is.Empty);
    }

    [Test]
    public async Task CommitAsync_propagatesCancellationInsteadOfLeavingGitRunning()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "README.md"), "widgets changed\n");
        var provider = new GitWorkingTreeProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(
            Assert.CatchAsync(async () => await provider.CommitAsync(
                _repository,
                ["README.md"],
                "fix(App): change readme",
                cancellation.Token)),
            Is.AssignableTo<OperationCanceledException>());
    }

    [Test]
    public void CommitAsync_rejectsAnEmptyFileSelection()
    {
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(_repository, [], "chore: nothing"));

        Assert.That(exception!.Message, Does.Contain("at least one file"));
    }

    [Test]
    public void GetChangesAsync_rejectsMalformedWorktreePathsBeforeLaunchingGit()
    {
        var provider = new GitWorkingTreeProvider();
        var path = Path.Join(TestContext.CurrentContext.WorkDirectory, "repo") + "\n--upload-pack=malicious";

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetChangesAsync(path));

        Assert.That(exception!.Message, Does.Contain("worktree path"));
    }
}
