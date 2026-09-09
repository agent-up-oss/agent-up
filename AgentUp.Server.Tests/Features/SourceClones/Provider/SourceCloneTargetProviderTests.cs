using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Providers;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.SourceClones.Provider;

[TestFixture]
public sealed class SourceCloneTargetProviderTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), $"agent-up-clone-root-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestCase("https://example.test/acme/widgets.git", "widgets")]
    [TestCase("https://example.test/acme/widgets", "widgets")]
    [TestCase("http://example.test/acme/widgets.git", "widgets")]
    [TestCase("ssh://git@example.test/acme/widgets.git", "widgets")]
    [TestCase("git@example.test:acme/widgets.git", "widgets")]
    [TestCase("https://example.test/acme/widgets.git/", "widgets")]
    public void Resolve_derivesTheCloneDirectoryFromTheRepositoryName(string repository, string expected)
    {
        var provider = CreateProvider();

        var target = provider.Resolve(new CloneSourceRequest(repository, "main"));

        Assert.That(target.DirectoryName, Is.EqualTo(expected));
        Assert.That(target.DestinationPath, Is.EqualTo(Path.Join(Path.GetFullPath(_root), expected)));
    }

    [Test]
    public void Resolve_trimsSurroundingWhitespaceFromRepositoryAndBranch()
    {
        var provider = CreateProvider();

        var target = provider.Resolve(new CloneSourceRequest("  https://example.test/acme/widgets.git  ", " feature/login "));

        Assert.That(target.Repository, Is.EqualTo("https://example.test/acme/widgets.git"));
        Assert.That(target.Branch, Is.EqualTo("feature/login"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("widgets")]
    [TestCase("acme/widgets")]
    [TestCase("file:///etc/passwd")]
    [TestCase("--upload-pack=touch /tmp/pwned")]
    [TestCase("ext::sh -c whoami")]
    public void Resolve_rejectsRepositoriesThatAreNotRemoteUrls(string repository)
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.Resolve(new CloneSourceRequest(repository, "main")));

        Assert.That(exception!.Message, Does.Contain("Repository"));
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("--upload-pack")]
    [TestCase("feature/..")]
    [TestCase("feature/login.lock")]
    [TestCase("main@{upstream}")]
    [TestCase("feature login")]
    [TestCase("feature~1")]
    [TestCase("/main")]
    [TestCase("main/")]
    public void Resolve_rejectsBranchesThatAreNotValidGitBranchNames(string branch)
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.Resolve(new CloneSourceRequest("https://example.test/acme/widgets.git", branch)));

        Assert.That(exception!.Message, Does.Contain("Branch"));
    }

    [Test]
    public void Resolve_keepsTraversalAttemptsUnderTheSourceClonesRoot()
    {
        var provider = CreateProvider();

        var target = provider.Resolve(new CloneSourceRequest("https://example.test/acme/..%2f..%2fetc.git", "main"));

        Assert.That(Path.GetDirectoryName(target.DestinationPath), Is.EqualTo(Path.GetFullPath(_root)));
    }

    [Test]
    public void DestinationExists_isFalseUntilTheDirectoryIsCreated()
    {
        var provider = CreateProvider();
        var target = provider.Resolve(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        Assert.That(provider.DestinationExists(target), Is.False);

        Directory.CreateDirectory(target.DestinationPath);

        Assert.That(provider.DestinationExists(target), Is.True);
    }

    private SourceCloneTargetProvider CreateProvider()
        => new(new FakeSourceCloneRootProvider(_root));
}
