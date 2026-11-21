using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Git.Provider;

/// <summary>
/// The commits and checkouts the Server makes around an autonomous team member, in a real
/// repository: an author's work onto its task branch, and a reviewer onto exactly the revision.
/// </summary>
[TestFixture]
public sealed class GitTaskBranchProviderTests
{
    private static readonly GitIdentity Bob = new("Bob", "bob@agent-up.invalid");

    [Test]
    public async Task CommitAllToBranchAsync_carriesTheWorkOntoANewTaskBranchUnderTheMember()
    {
        var repository = await RepositoryAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Ahoy\n");
        await File.WriteAllTextAsync(Path.Join(repository, "new.txt"), "added\n");

        var commit = await new GitWorkingTreeProvider().CommitAllToBranchAsync(repository, "agent-up/issue-1", "Say Ahoy (#1)", Bob);

        Assert.Multiple(async () =>
        {
            Assert.That(commit, Is.Not.Null);
            Assert.That(await TestGitRepository.ReadAsync(repository, "rev-parse", "--abbrev-ref", "HEAD"), Is.EqualTo("agent-up/issue-1"));
            Assert.That(await TestGitRepository.ReadAsync(repository, "log", "-1", "--format=%an <%ae>|%s"), Is.EqualTo("Bob <bob@agent-up.invalid>|Say Ahoy (#1)"));
            Assert.That(await TestGitRepository.ReadAsync(repository, "status", "--porcelain"), Is.Empty, "untracked files are part of the work");
            Assert.That(await TestGitRepository.ReadAsync(repository, "log", "-1", "--format=%s", "main"), Is.EqualTo("initial"), "the base branch is not moved");
        });
        Directory.Delete(repository, recursive: true);
    }

    [Test]
    public async Task ResolveCommitAsync_namesTheCommitARefPointsAtAndNothingForAMissingRef()
    {
        var repository = await RepositoryAsync();
        var provider = new GitWorkingTreeProvider();

        var head = await provider.ResolveCommitAsync(repository, "HEAD");
        var missing = await provider.ResolveCommitAsync(repository, "refs/remotes/origin/agent-up/issue-9");

        Assert.Multiple(async () =>
        {
            Assert.That(head, Is.EqualTo(await TestGitRepository.ReadAsync(repository, "rev-parse", "HEAD")));
            Assert.That(missing, Is.Null);
        });
        Directory.Delete(repository, recursive: true);
    }

    [Test]
    public async Task CommitAllToBranchAsync_leavesTheCheckoutAloneWhenNothingChanged()
    {
        var repository = await RepositoryAsync();

        var commit = await new GitWorkingTreeProvider().CommitAllToBranchAsync(repository, "agent-up/issue-1", "Nothing", Bob);

        Assert.Multiple(async () =>
        {
            Assert.That(commit, Is.Null);
            Assert.That(await TestGitRepository.ReadAsync(repository, "rev-parse", "--abbrev-ref", "HEAD"), Is.EqualTo("main"));
        });
        Directory.Delete(repository, recursive: true);
    }

    // A ticket's branch exists from the moment it is taken, before any commit; a branch left over
    // from an earlier attempt under the same name starts again from the base.
    [Test]
    public async Task StartBranchAsync_startsTheTaskBranchAtTheBaseReplacingAStaleOne()
    {
        var repository = await RepositoryAsync();
        var provider = new GitWorkingTreeProvider();
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Stale\n");
        await provider.CommitAllToBranchAsync(repository, "agent-up/issue-1", "Stale", Bob);
        await TestGitRepository.RunAsync(repository, "switch", "main");

        await provider.StartBranchAsync(repository, "agent-up/issue-1");

        Assert.Multiple(async () =>
        {
            Assert.That(await TestGitRepository.ReadAsync(repository, "rev-parse", "--abbrev-ref", "HEAD"), Is.EqualTo("agent-up/issue-1"));
            Assert.That(await TestGitRepository.ReadAsync(repository, "rev-parse", "HEAD"), Is.EqualTo(await TestGitRepository.ReadAsync(repository, "rev-parse", "main")));
        });
        Directory.Delete(repository, recursive: true);
    }

    [Test]
    public async Task CommitAllToBranchAsync_addsARevisionToAnExistingTaskBranch()
    {
        var repository = await RepositoryAsync();
        var provider = new GitWorkingTreeProvider();
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Ahoy\n");
        await provider.CommitAllToBranchAsync(repository, "agent-up/issue-1", "First", Bob);
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Ahoy there\n");

        await provider.CommitAllToBranchAsync(repository, "agent-up/issue-1", "Second", Bob);

        Assert.That(await TestGitRepository.ReadAsync(repository, "log", "--format=%s", "main..agent-up/issue-1"), Is.EqualTo("Second\nFirst"));
        Directory.Delete(repository, recursive: true);
    }

    [Test]
    public async Task ResetToRemoteBranchAsync_checksOutTheFetchedRevisionAndDiffShowsWhatItAdds()
    {
        var repository = await RepositoryAsync();
        var provider = new GitWorkingTreeProvider();
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Ahoy\n");
        var commit = await provider.CommitAllToBranchAsync(repository, "agent-up/issue-1", "Say Ahoy", Bob);
        await TestGitRepository.RunAsync(repository, "update-ref", "refs/remotes/origin/agent-up/issue-1", commit!);
        await TestGitRepository.RunAsync(repository, "update-ref", "refs/remotes/origin/main", "main");
        await TestGitRepository.RunAsync(repository, "switch", "main");

        await provider.ResetToRemoteBranchAsync(repository, "agent-up/issue-1");
        var diff = await provider.GetRangeDiffAsync(repository, "refs/remotes/origin/main", "HEAD");

        Assert.Multiple(async () =>
        {
            Assert.That(await TestGitRepository.ReadAsync(repository, "rev-parse", "HEAD"), Is.EqualTo(commit));
            Assert.That(diff, Does.Contain("-Hello").And.Contain("+Ahoy"));
        });
        Directory.Delete(repository, recursive: true);
    }

    [Test]
    public async Task CommitAllToBranchAsync_refusesABranchNameGitWouldNotAccept()
    {
        var repository = await RepositoryAsync();
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Ahoy\n");

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GitWorkingTreeProvider().CommitAllToBranchAsync(repository, "agent-up/..bad", "x", Bob));
        Directory.Delete(repository, recursive: true);
    }

    private static async Task<string> RepositoryAsync()
    {
        var repository = Path.Join(TestContext.CurrentContext.WorkDirectory, $"git-task-{Guid.NewGuid():N}");
        await TestGitRepository.InitializeAsync(repository);
        await File.WriteAllTextAsync(Path.Join(repository, "greeting.txt"), "Hello\n");
        await TestGitRepository.CommitAllAsync(repository, "initial");
        return repository;
    }
}
