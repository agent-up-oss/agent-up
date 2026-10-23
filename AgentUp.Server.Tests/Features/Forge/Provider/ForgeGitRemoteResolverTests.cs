using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeGitRemoteResolverTests
{
    [Test]
    public void Resolve_readsAnHttpsGitHubOrigin()
        => AssertRemote("https://github.com/acme/widgets.git", "acme", "widgets", "main");

    [Test]
    public void Resolve_readsAnSshGitHubOrigin()
        => AssertRemote("git@github.com:acme/widgets.git", "acme", "widgets", "topic", "topic");

    [Test]
    public void Resolve_readsAnHttpsForgejoOrigin()
        => AssertRemote("https://git.example.test/acme/widgets.git", "acme", "widgets", "main");

    [Test]
    public void Resolve_returnsNullForAFileRemote()
    {
        var root = WriteCheckout("file:///var/repos/acme/widgets.git", "main");
        try
        {
            Assert.That(new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Resolve_returnsNullWhenGitIsMissing()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Assert.That(new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Resolve_returnsNullWhenOriginHasNoUrl()
    {
        var root = WriteCheckout("https://github.com/acme/widgets.git", "main");
        try
        {
            File.WriteAllText(Path.Join(root, ".git", "config"), "[remote \"origin\"]\n\tfetch = +refs/heads/*\n");
            Assert.That(new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Resolve_returnsAnEmptyBranchWhenHeadIsMissing()
    {
        var root = WriteCheckout("https://github.com/acme/widgets.git", "main");
        try
        {
            File.Delete(Path.Join(root, ".git", "HEAD"));
            var remote = new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root);
            Assert.That(remote!.Branch, Is.Empty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Resolve_returnsNullWhenConfigIsMissing()
    {
        var root = WriteCheckout("https://github.com/acme/widgets.git", "main");
        try
        {
            File.Delete(Path.Join(root, ".git", "config"));
            Assert.That(new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TryParse_rejectsAUrlWithNoRepository()
        => Assert.That(ForgeGitRemoteResolver.TryParse("https://git.example.test/acme", out _, out _), Is.False);

    private static void AssertRemote(string url, string owner, string repository, string branch, string head = "main")
    {
        var root = WriteCheckout(url, head);
        try
        {
            var remote = new ForgeGitRemoteResolver(new GitDirectoryProvider()).Resolve(root);
            Assert.Multiple(() =>
            {
                Assert.That(remote!.Owner, Is.EqualTo(owner));
                Assert.That(remote.Repository, Is.EqualTo(repository));
                Assert.That(remote.Branch, Is.EqualTo(branch));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteCheckout(string url, string branch)
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"forge-remote-{Guid.NewGuid():N}");
        var git = Path.Join(root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Join(git, "HEAD"), $"ref: refs/heads/{branch}\n");
        File.WriteAllText(Path.Join(git, "config"), $"[remote \"origin\"]\n\turl = {url}\n");
        return root;
    }
}
