using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitWorkingTreeProviderDeepTests
{
    private readonly List<string> _directories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
            Directory.Delete(directory, recursive: true);

        _directories.Clear();
    }

    [Test]
    public async Task GetChangesAsync_returnsAnEmptyListForACleanWorktree()
    {
        var repository = await SeedAsync();
        var provider = new GitWorkingTreeProvider();

        Assert.That(await provider.GetChangesAsync(repository), Is.Empty);
    }

    [Test]
    public async Task GetChangesAsync_reportsConflictedPaths()
    {
        var repository = await SeedConflictAsync();
        var provider = new GitWorkingTreeProvider();

        var changes = await provider.GetChangesAsync(repository);

        Assert.That(changes.Select(change => change.Path), Does.Contain("conflict.txt"));
        Assert.That(changes.Single(change => change.Path == "conflict.txt").Status, Is.EqualTo(GitChangeStatus.Conflicted));
    }

    [Test]
    public async Task GetFileDiffAsync_marksBinaryFiles()
    {
        var repository = await SeedAsync();
        await File.WriteAllBytesAsync(Path.Join(repository, "blob.bin"), [0x00, 0x01, 0x02, 0xff]);
        var provider = new GitWorkingTreeProvider();

        var diff = await provider.GetFileDiffAsync(repository, "blob.bin");

        Assert.That(diff, Is.Not.Null);
        Assert.That(diff!.IsBinary, Is.True);
        Assert.That(diff.Status, Is.EqualTo(GitChangeStatus.Untracked));
    }

    [Test]
    public async Task GetFileDiffAsync_returnsNullForAMissingPath()
    {
        var repository = await SeedAsync();
        var provider = new GitWorkingTreeProvider();

        Assert.That(await provider.GetFileDiffAsync(repository, "missing.cs"), Is.Null);
    }

    [Test]
    public async Task CommitAsync_commitsUntrackedFiles()
    {
        var repository = await SeedAsync();
        Directory.CreateDirectory(Path.Join(repository, "docs", "guide"));
        await File.WriteAllTextAsync(Path.Join(repository, "docs", "guide", "a.md"), "a\n");
        var provider = new GitWorkingTreeProvider();

        var commit = await provider.CommitAsync(repository, ["docs/guide/a.md"], "feat(Docs): add guide");

        Assert.That(commit, Has.Length.EqualTo(40));
        Assert.That(await provider.GetChangesAsync(repository), Is.Empty);
    }

    [Test]
    public async Task CommitAsync_stillCommitsWhenOnlyTheRepositoryIdentityIsConfigured()
    {
        var repository = await SeedAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "changed\n");
        var provider = new GitWorkingTreeProvider();

        using var isolation = TestGitConfigIsolation.Begin();
        var commit = await provider.CommitAsync(repository, ["README.md"], "fix(App): change readme");

        Assert.That(commit, Has.Length.EqualTo(40));
    }

    [Test]
    public async Task CommitAsync_reportsAStructuredErrorWhenIdentityIsMissing()
    {
        var repository = await SeedAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "changed\n");
        await TestGitRepository.UnsetIdentityAsync(repository);
        var provider = new GitWorkingTreeProvider();

        using var isolation = TestGitConfigIsolation.Begin();
        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(repository, ["README.md"], "fix(App): change readme"));

        Assert.That(exception!.Message, Does.Contain("user.name").And.Contain("user.email"));
        Assert.That(exception.Message, Does.Not.Contain("Please tell me who you are"));
        Assert.That(exception.Message, Does.Not.Contain("***"));
    }

    [Test]
    public async Task CommitAsync_reportsAStructuredErrorDuringAnInProgressMerge()
    {
        var repository = await SeedConflictAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "other\n");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CommitAsync(repository, ["README.md"], "fix(App): unrelated"));

        Assert.That(exception!.Message, Does.Contain("in-progress merge"));
        Assert.That(exception.Message, Does.Not.Contain("fatal:"));
    }

    [Test]
    public async Task DiscardAsync_deletesOnlyTheSelectedUntrackedFile()
    {
        var repository = await SeedAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "keep.md"), "keep\n");
        await File.WriteAllTextAsync(Path.Join(repository, "drop.md"), "drop\n");
        var provider = new GitWorkingTreeProvider();

        await provider.DiscardAsync(repository, ["drop.md"]);

        Assert.That(File.Exists(Path.Join(repository, "keep.md")), Is.True);
        Assert.That(File.Exists(Path.Join(repository, "drop.md")), Is.False);
    }

    [Test]
    public async Task SwitchBranchAsync_refusesADirtyWorktreeWithAStructuredError()
    {
        var repository = await SeedAsync();
        await TestGitRepository.RunAsync(repository, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "topic\n");
        await TestGitRepository.CommitAllAsync(repository, "topic");
        await TestGitRepository.RunAsync(repository, "switch", "main");
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "dirty\n");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.SwitchBranchAsync(repository, "topic", create: false));

        Assert.That(exception!.Message, Does.Contain("conflicting local changes"));
        Assert.That(exception.Message, Does.Not.Contain("Please commit your changes or stash them"));
        Assert.That((await provider.GetHeadStateAsync(repository)).Branch, Is.EqualTo("main"));
    }

    [Test]
    public async Task CheckoutRemoteAsync_rejectsANameThatExistsOnMultipleRemotes()
    {
        var origin = await CreateRemoteWithTopicAsync("origin");
        var other = await CreateRemoteWithTopicAsync("other");
        var repository = await SeedAsync();
        await TestGitRepository.RunAsync(repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(repository, "remote", "add", "other", other);
        await TestGitRepository.RunAsync(repository, "fetch", "origin");
        await TestGitRepository.RunAsync(repository, "fetch", "other");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CheckoutRemoteAsync(repository, "topic"));

        Assert.That(exception!.Message, Does.Contain("multiple remotes"));
    }

    [Test]
    public async Task PullAsync_reportsAStructuredErrorWhenHistoriesDiverge()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        var other = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-other"));
        await TestGitRepository.RunAsync(Path.GetDirectoryName(other)!, "clone", bare, other);
        await TestGitRepository.ConfigureIdentityAsync(other);
        await File.WriteAllTextAsync(Path.Join(other, "other.md"), "other\n");
        await TestGitRepository.CommitAllAsync(other, "other");
        await TestGitRepository.RunAsync(other, "push", "origin", "main");
        await File.WriteAllTextAsync(Path.Join(clone, "clone.md"), "clone\n");
        await TestGitRepository.CommitAllAsync(clone, "clone");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PullAsync(clone, rebase: false));

        Assert.That(exception!.Message, Does.Contain("fast-forward"));
        Assert.That(exception.Message, Does.Not.Contain("Not possible to fast-forward"));
        Assert.That(exception.Message, Does.Not.Contain("fatal:"));
    }

    [Test]
    public async Task PushAsync_reportsAStructuredErrorWhenTheBranchHasNoUpstream()
    {
        var (clone, _) = await CreateLinkedClonePairAsync();
        await TestGitRepository.RunAsync(clone, "branch", "--unset-upstream");
        await File.WriteAllTextAsync(Path.Join(clone, "later.md"), "later\n");
        await TestGitRepository.CommitAllAsync(clone, "later");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PushAsync(clone, forceWithLease: false, setUpstream: false));

        Assert.That(exception!.Message, Does.Contain("no upstream"));
        Assert.That(exception.Message, Does.Not.Contain("fatal:"));
    }

    [Test]
    public async Task PushAsync_forceWithLeasePublishesWhenTheLeaseMatches()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        await File.WriteAllTextAsync(Path.Join(clone, "lease.md"), "lease\n");
        await TestGitRepository.CommitAllAsync(clone, "lease");
        var provider = new GitWorkingTreeProvider();

        await provider.PushAsync(clone, forceWithLease: true, setUpstream: false);

        Assert.That(await TestGitRepository.ReadAsync(bare, "log", "-1", "--pretty=%s"), Is.EqualTo("lease"));
    }

    [Test]
    public async Task PushAsync_forceWithLeaseFailsWhenTheRemoteMoved()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        var other = Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-other");
        Track(other);
        await TestGitRepository.RunAsync(Path.GetDirectoryName(other)!, "clone", bare, other);
        await TestGitRepository.ConfigureIdentityAsync(other);
        await File.WriteAllTextAsync(Path.Join(other, "other.md"), "other\n");
        await TestGitRepository.CommitAllAsync(other, "other");
        await TestGitRepository.RunAsync(other, "push", "origin", "main");
        await File.WriteAllTextAsync(Path.Join(clone, "stale.md"), "stale\n");
        await TestGitRepository.CommitAllAsync(clone, "stale");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PushAsync(clone, forceWithLease: true, setUpstream: false));

        Assert.That(exception!.Message,
            Does.Contain("Force-with-lease").Or.Contain("non-fast-forward"));
        Assert.That(exception.Message, Does.Not.Contain("stale info"));
    }

    [Test]
    public async Task PushAsync_reportsAuthenticationFailuresFromARejectingRemote()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        var hook = Path.Join(bare, "hooks", "pre-receive");
        await File.WriteAllTextAsync(hook, "#!/bin/sh\necho 'Permission denied (publickey).' >&2\nexit 1\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await File.WriteAllTextAsync(Path.Join(clone, "auth.md"), "auth\n");
        await TestGitRepository.CommitAllAsync(clone, "auth");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PushAsync(clone, forceWithLease: false, setUpstream: false));

        Assert.That(exception!.Message, Does.Contain("authenticate"));
        Assert.That(exception.Message, Does.Not.Contain("Permission denied (publickey)"));
    }

    [Test]
    public async Task GetLogAsync_clampsThePageSizeAndReportsHasMore()
    {
        var repository = await SeedAsync();
        for (var index = 1; index <= 12; index++)
        {
            await File.WriteAllTextAsync(Path.Join(repository, $"n{index}.md"), $"{index}\n");
            await TestGitRepository.CommitAllAsync(repository, $"commit {index}");
        }

        var provider = new GitWorkingTreeProvider();
        var page = await provider.GetLogAsync(repository, 200);
        var small = await provider.GetLogAsync(repository, 5);
        var skipped = await provider.GetLogAsync(repository, 5, skip: 5);
        var until = await provider.GetLogAsync(repository, 5, until: small.Commits[0].Id);

        Assert.That(page.Commits, Has.Count.EqualTo(13));
        Assert.That(page.HasMore, Is.False);
        Assert.That(small.Commits, Has.Count.EqualTo(5));
        Assert.That(small.HasMore, Is.True);
        Assert.That(small.Commits[0].Subject, Is.EqualTo("commit 12"));
        Assert.That(skipped.Commits[0].Subject, Is.EqualTo("commit 7"));
        Assert.That(until.Commits[0].Subject, Is.EqualTo("commit 11"));
        var oversized = await provider.GetLogAsync(repository, 500);
        Assert.That(oversized.Commits, Has.Count.EqualTo(13));
    }

    [Test]
    public async Task CheckoutRemoteAsync_switchesAnExistingLocalBranchAndAcceptsARemoteQualifiedName()
    {
        var origin = await CreateRemoteWithTopicAsync("origin");
        var repository = await SeedAsync();
        await TestGitRepository.RunAsync(repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(repository, "fetch", "origin");
        await TestGitRepository.RunAsync(repository, "switch", "-c", "topic");
        await TestGitRepository.RunAsync(repository, "switch", "main");
        var provider = new GitWorkingTreeProvider();

        await provider.CheckoutRemoteAsync(repository, "topic");
        Assert.That((await provider.GetHeadStateAsync(repository)).Branch, Is.EqualTo("topic"));

        await provider.CheckoutRemoteAsync(repository, "origin/topic");
        Assert.That((await provider.GetHeadStateAsync(repository)).Branch, Is.EqualTo("topic"));
    }

    [Test]
    public async Task PushAsync_setUpstreamPublishesABranchWithoutTracking()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        await TestGitRepository.RunAsync(clone, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(clone, "topic.md"), "topic\n");
        await TestGitRepository.CommitAllAsync(clone, "topic");
        var provider = new GitWorkingTreeProvider();

        await provider.PushAsync(clone, forceWithLease: false, setUpstream: true);

        Assert.That(await TestGitRepository.ReadAsync(bare, "rev-parse", "--abbrev-ref", "topic"), Is.EqualTo("topic"));
        var head = await provider.GetHeadStateAsync(clone);
        Assert.That(head.Upstream, Does.Contain("topic"));
    }

    [Test]
    public async Task PushAsync_setUpstreamRejectsDetachedHead()
    {
        var (clone, _) = await CreateLinkedClonePairAsync();
        await TestGitRepository.RunAsync(clone, "switch", "--detach");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PushAsync(clone, forceWithLease: false, setUpstream: true));

        Assert.That(exception!.Message, Does.Contain("checked-out branch"));
    }

    [Test]
    public async Task PushAsync_reportsANonFastForwardRejection()
    {
        var (clone, bare) = await CreateLinkedClonePairAsync();
        var other = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-ahead"));
        await TestGitRepository.RunAsync(Path.GetDirectoryName(other)!, "clone", bare, other);
        await TestGitRepository.ConfigureIdentityAsync(other);
        await File.WriteAllTextAsync(Path.Join(other, "other.md"), "other\n");
        await TestGitRepository.CommitAllAsync(other, "other");
        await TestGitRepository.RunAsync(other, "push", "origin", "main");
        await File.WriteAllTextAsync(Path.Join(clone, "stale.md"), "stale\n");
        await TestGitRepository.CommitAllAsync(clone, "stale");
        var provider = new GitWorkingTreeProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.PushAsync(clone, forceWithLease: false, setUpstream: false));

        Assert.That(exception!.Message, Does.Contain("non-fast-forward"));
        Assert.That(exception.Message, Does.Not.Contain("failed to push some refs"));
    }

    [Test]
    public async Task GetLogAsync_normalizesHeadTagsAndRemoteRefs()
    {
        var repository = await SeedAsync();
        await TestGitRepository.RunAsync(repository, "tag", "v1.0.0");
        await TestGitRepository.RunAsync(repository, "update-ref", "refs/remotes/origin/main", "HEAD");
        var provider = new GitWorkingTreeProvider();

        var log = await provider.GetLogAsync(repository, 20);

        Assert.That(log.Commits[0].Refs, Does.Contain("HEAD").Or.Contain("main"));
        Assert.That(log.Commits[0].Refs, Does.Contain("v1.0.0"));
        Assert.That(log.Commits[0].Refs, Does.Contain("origin/main"));
    }

    private async Task<string> SeedAsync()
    {
        var repository = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}"));
        await TestGitRepository.InitializeAsync(repository);
        await File.WriteAllTextAsync(Path.Join(repository, "README.md"), "widgets\n");
        await TestGitRepository.CommitAllAsync(repository, "initial");
        return repository;
    }

    private async Task<string> SeedConflictAsync()
    {
        var repository = await SeedAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "conflict.txt"), "base\n");
        await TestGitRepository.CommitAllAsync(repository, "conflict base");
        await TestGitRepository.RunAsync(repository, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(repository, "conflict.txt"), "topic\n");
        await TestGitRepository.CommitAllAsync(repository, "topic");
        await TestGitRepository.RunAsync(repository, "switch", "main");
        await File.WriteAllTextAsync(Path.Join(repository, "conflict.txt"), "main\n");
        await TestGitRepository.CommitAllAsync(repository, "main");
        await TestGitRepository.RunAsync(repository, ["merge", "topic"], [1]);
        return repository;
    }

    private async Task<string> CreateRemoteWithTopicAsync(string label)
    {
        var origin = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-{label}"));
        await TestGitRepository.InitializeAsync(origin);
        await File.WriteAllTextAsync(Path.Join(origin, "ORIGIN.md"), $"{label}\n");
        await TestGitRepository.CommitAllAsync(origin, label);
        await TestGitRepository.RunAsync(origin, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(origin, "TOPIC.md"), "topic\n");
        await TestGitRepository.CommitAllAsync(origin, "topic");
        await TestGitRepository.RunAsync(origin, "switch", "main");
        return origin;
    }

    private async Task<(string Clone, string Bare)> CreateLinkedClonePairAsync()
    {
        var work = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-origin-work"));
        await TestGitRepository.InitializeAsync(work);
        await File.WriteAllTextAsync(Path.Join(work, "README.md"), "origin\n");
        await TestGitRepository.CommitAllAsync(work, "initial");
        var bare = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-origin.git"));
        await TestGitRepository.RunAsync(Path.GetDirectoryName(bare)!, "clone", "--bare", work, bare);
        var clone = Track(Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-deep-{Guid.NewGuid():N}-clone"));
        await TestGitRepository.RunAsync(Path.GetDirectoryName(clone)!, "clone", bare, clone);
        await TestGitRepository.ConfigureIdentityAsync(clone);
        return (clone, bare);
    }

    private string Track(string path)
    {
        _directories.Add(path);
        return path;
    }
}
