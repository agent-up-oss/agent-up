using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class GitDirectoryProviderTests
{
    private static string CreateRoot()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, "gitdir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Test]
    public void Resolve_returnsTheGitDirectoryOfAnOrdinaryCheckout()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(Path.Join(root, ".git"));

        Assert.That(new GitDirectoryProvider().Resolve(root), Is.EqualTo(Path.Join(root, ".git")));
    }

    [Test]
    public void Resolve_followsThePointerFileALinkedWorktreeUsesSoEachWorktreeKeepsItsOwnReceipts()
    {
        var root = CreateRoot();
        var actualGitDirectory = Path.Join(root, "actual-git");
        Directory.CreateDirectory(actualGitDirectory);
        File.WriteAllText(Path.Join(root, ".git"), $"gitdir: {actualGitDirectory}\n");

        Assert.That(new GitDirectoryProvider().Resolve(root), Is.EqualTo(actualGitDirectory));
    }

    [Test]
    public void Resolve_resolvesARelativePointerAgainstTheCheckout()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(Path.Join(root, "nested-git"));
        File.WriteAllText(Path.Join(root, ".git"), "gitdir: nested-git\n");

        Assert.That(new GitDirectoryProvider().Resolve(root), Is.EqualTo(Path.Join(root, "nested-git")));
    }

    [Test]
    public void Resolve_returnsNullWhenTheDirectoryIsNotACheckout()
    {
        Assert.That(new GitDirectoryProvider().Resolve(CreateRoot()), Is.Null);
    }
}
