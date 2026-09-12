using AgentUp.Server.Features.Workspaces.Providers;

namespace AgentUp.Server.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceDiskUsageProviderTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), $"agent-up-disk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Measure_returnsZero_whenPathIsMissing()
    {
        var provider = new WorkspaceDiskUsageProvider();

        Assert.That(provider.Measure(Path.Join(_root, "missing")), Is.EqualTo(0));
    }

    [Test]
    public void Measure_sumsNestedFiles()
    {
        Directory.CreateDirectory(Path.Join(_root, "src"));
        File.WriteAllBytes(Path.Join(_root, "readme.txt"), new byte[12]);
        File.WriteAllBytes(Path.Join(_root, "src", "app.bin"), new byte[20]);
        var provider = new WorkspaceDiskUsageProvider();

        Assert.That(provider.Measure(_root), Is.EqualTo(32));
    }
}
