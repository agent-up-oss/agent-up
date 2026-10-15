using AgentUp.Server.Features.SourceClones.Providers;

namespace AgentUp.Server.Tests.Features.SourceClones.Provider;

[TestFixture]
public sealed class SourceCloneRootProviderTests
{
    private string? _previousRoot;

    [SetUp]
    public void SetUp()
    {
        _previousRoot = Environment.GetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, _previousRoot);
    }

    [Test]
    public void GetRoot_fallsBackToASourcesDirectoryUnderTheDataDirectory()
    {
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, null);
        var dataDirectory = Path.Join(Path.GetTempPath(), "agent-up-data");
        var provider = new SourceCloneRootProvider(dataDirectory);

        Assert.That(
            provider.GetRoot(),
            Is.EqualTo(Path.GetFullPath(Path.Join(dataDirectory, SourceCloneRootProvider.DefaultDirectoryName))));
    }

    [Test]
    public void GetRoot_prefersTheInjectedEnvironmentVariable()
    {
        var configured = Path.Join(Path.GetTempPath(), "agent-up-managed-sources");
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, configured);
        var provider = new SourceCloneRootProvider(Path.Join(Path.GetTempPath(), "agent-up-data"));

        Assert.That(provider.GetRoot(), Is.EqualTo(Path.GetFullPath(configured)));
    }

    [Test]
    public void GetRoot_ignoresABlankEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, "   ");
        var dataDirectory = Path.Join(Path.GetTempPath(), "agent-up-data");
        var provider = new SourceCloneRootProvider(dataDirectory);

        Assert.That(
            provider.GetRoot(),
            Is.EqualTo(Path.GetFullPath(Path.Join(dataDirectory, SourceCloneRootProvider.DefaultDirectoryName))));
    }

    [Test]
    public void IsUnderRoot_rejectsPathsOutsideTheConfiguredRoot()
    {
        var root = Path.Join(Path.GetTempPath(), $"agent-up-clone-root-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, root);
        var provider = new SourceCloneRootProvider(Path.Join(Path.GetTempPath(), "agent-up-data"));

        Assert.That(provider.IsUnderRoot(Path.Join(root, "widgets")), Is.True);
        Assert.That(provider.IsUnderRoot(Path.Join(Path.GetTempPath(), "elsewhere")), Is.False);
    }

    [Test]
    public void IsUnderRoot_rejectsABlankPath()
    {
        var provider = new SourceCloneRootProvider(Path.Join(Path.GetTempPath(), "agent-up-data"));

        Assert.That(provider.IsUnderRoot("   "), Is.False);
    }

    [Test]
    public void DeleteIfUnderRoot_removesADirectoryInsideTheRootAndLeavesOthers()
    {
        var root = Path.Join(Path.GetTempPath(), $"agent-up-clone-root-{Guid.NewGuid():N}");
        var inside = Path.Join(root, "widgets-review-topic");
        var outside = Path.Join(Path.GetTempPath(), $"agent-up-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(inside);
        Directory.CreateDirectory(outside);
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, root);
        var provider = new SourceCloneRootProvider(Path.Join(Path.GetTempPath(), "agent-up-data"));

        provider.DeleteIfUnderRoot(inside);
        provider.DeleteIfUnderRoot(outside);

        Assert.That(Directory.Exists(inside), Is.False);
        Assert.That(Directory.Exists(outside), Is.True);
        Directory.Delete(outside, recursive: true);
    }
}
