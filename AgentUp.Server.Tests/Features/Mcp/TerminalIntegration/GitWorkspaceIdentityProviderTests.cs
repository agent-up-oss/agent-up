using AgentUp.Server.Features.Mcp.Providers;

namespace AgentUp.Server.Tests.Features.Mcp.TerminalIntegration;

[TestFixture]
public sealed class GitWorkspaceIdentityProviderTests
{
    [Test]
    public async Task ReadAsync_ReturnsGitIdentity_ForRepository()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-git-identity-");
        try
        {
            var commit = "0123456789abcdef0123456789abcdef01234567";
            await WriteGitHeadAsync(root.FullName, "main", commit);

            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(root.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(root.FullName));
            Assert.That(identity.Branch, Is.EqualTo("main"));
            Assert.That(identity.Commit, Is.EqualTo(commit));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsRepositoryRoot_ForNestedRepositoryPath()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-nested-git-identity-");
        try
        {
            var nested = Directory.CreateDirectory(Path.Join(root.FullName, "src", "App"));
            await WriteGitHeadAsync(root.FullName, "feature/test", "1111111111111111111111111111111111111111");

            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(nested.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(root.FullName));
            Assert.That(identity.Branch, Is.EqualTo("feature/test"));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsMainRepositoryRoot_ForLinkedWorktree()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-linked-worktree-");
        try
        {
            var mainRepository = Directory.CreateDirectory(Path.Join(root.FullName, "repo"));
            var worktree = Directory.CreateDirectory(Path.Join(root.FullName, "repo-worktree"));
            var commonDirectory = Directory.CreateDirectory(Path.Join(mainRepository.FullName, ".git"));
            var gitDirectory = Directory.CreateDirectory(Path.Join(commonDirectory.FullName, "worktrees", "repo-worktree"));
            var commit = "2222222222222222222222222222222222222222";

            await File.WriteAllTextAsync(Path.Join(worktree.FullName, ".git"), $"gitdir: {gitDirectory.FullName}\n");
            await File.WriteAllTextAsync(Path.Join(gitDirectory.FullName, "commondir"), "../..\n");
            await File.WriteAllTextAsync(Path.Join(gitDirectory.FullName, "HEAD"), "ref: refs/heads/worktree-branch\n");
            Directory.CreateDirectory(Path.Join(commonDirectory.FullName, "refs", "heads"));
            await File.WriteAllTextAsync(Path.Join(commonDirectory.FullName, "refs", "heads", "worktree-branch"), commit);

            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(worktree.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(mainRepository.FullName));
            Assert.That(identity.Branch, Is.EqualTo("worktree-branch"));
            Assert.That(identity.Commit, Is.EqualTo(commit));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsFallbackIdentity_ForNonGitDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-non-git-identity-");
        try
        {
            var provider = new GitWorkspaceIdentityProvider();

            var identity = await provider.ReadAsync(root.FullName, CancellationToken.None);

            Assert.That(identity.RepositoryPath, Is.EqualTo(root.FullName));
            Assert.That(identity.Branch, Is.EqualTo("not on a git branch"));
            Assert.That(identity.Commit, Is.Empty);
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_ReturnsFallbackIdentity_ForMissingDirectory()
    {
        var worktreePath = Path.Join(Path.GetTempPath(), $"agent-up-missing-git-identity-{Guid.NewGuid():N}");
        var provider = new GitWorkspaceIdentityProvider();

        var identity = await provider.ReadAsync(worktreePath, CancellationToken.None);

        Assert.That(identity.RepositoryPath, Is.EqualTo(worktreePath));
        Assert.That(identity.Branch, Is.EqualTo("not on a git branch"));
        Assert.That(identity.Commit, Is.Empty);
    }

    private static async Task WriteGitHeadAsync(string repositoryPath, string branch, string commit)
    {
        var gitDirectory = Directory.CreateDirectory(Path.Join(repositoryPath, ".git"));
        Directory.CreateDirectory(Path.Join(gitDirectory.FullName, "refs", "heads", Path.GetDirectoryName(branch) ?? ""));
        await File.WriteAllTextAsync(Path.Join(gitDirectory.FullName, "HEAD"), $"ref: refs/heads/{branch}\n");
        await File.WriteAllTextAsync(Path.Join(gitDirectory.FullName, "refs", "heads", branch), commit);
    }
}
