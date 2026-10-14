using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class GitRevisionProviderTests
{
    [Test]
    public void Resolve_readsTheHeadCommitFromAGitDirectory()
    {
        var root = CreateRepository("deadbeef0123456789abcdef0123456789abcd");
        try
        {
            Assert.That(new GitRevisionProvider(new GitDirectoryProvider()).Resolve(root),
                Is.EqualTo("deadbeef0123456789abcdef0123456789abcd"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Resolve_returnsUnknownWhenTheDirectoryIsNotACheckout()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-export-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.That(new GitRevisionProvider(new GitDirectoryProvider()).Resolve(root), Is.EqualTo("unknown"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRepository(string sha)
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-export-" + Guid.NewGuid().ToString("N"))).FullName;
        var git = Path.Join(root, ".git");
        Directory.CreateDirectory(Path.Join(git, "refs", "heads"));
        File.WriteAllText(Path.Join(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Join(git, "refs", "heads", "main"), sha + "\n");
        return root;
    }
}
