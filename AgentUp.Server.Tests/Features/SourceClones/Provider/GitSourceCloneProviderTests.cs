using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.SourceClones.Provider;

[TestFixture]
public sealed class GitSourceCloneProviderTests
{
    private string _root = null!;
    private string _origin = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"clone-provider-{Guid.NewGuid():N}");
        _origin = Path.Join(_root, "origin");
        await TestGitRepository.InitializeAsync(_origin);
        await File.WriteAllTextAsync(Path.Join(_origin, "README.md"), "widgets\n");
        await TestGitRepository.CommitAllAsync(_origin, "initial");
        await TestGitRepository.RunAsync(_origin, "checkout", "-b", "feature/login");
        await File.WriteAllTextAsync(Path.Join(_origin, "login.md"), "login\n");
        await TestGitRepository.CommitAllAsync(_origin, "login");
        await TestGitRepository.RunAsync(_origin, "checkout", "main");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task CloneAsync_checksOutTheRequestedBranchIntoTheSourceClonesRoot()
    {
        var destination = Path.Join(_root, "clones", "widgets");
        var provider = new GitSourceCloneProvider();

        await provider.CloneAsync(new SourceCloneTarget(_origin, "feature/login", "widgets", destination));

        Assert.That(File.Exists(Path.Join(destination, "login.md")), Is.True);
        Assert.That(await TestGitRepository.ReadAsync(destination, "rev-parse", "--abbrev-ref", "HEAD"),
            Is.EqualTo("feature/login"));
    }

    [Test]
    public void CloneAsync_reportsUnknownBranchesAsStructuredFailures()
    {
        var destination = Path.Join(_root, "clones", "widgets");
        var provider = new GitSourceCloneProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CloneAsync(new SourceCloneTarget(_origin, "does-not-exist", "widgets", destination)));

        Assert.That(exception!.Message, Does.Contain("does-not-exist"));
    }
}
