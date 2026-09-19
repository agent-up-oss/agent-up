using AgentUp.Server.Features.Ports.Models;
using AgentUp.Server.Features.Ports.Providers;

namespace AgentUp.Server.Tests.Features.Ports.Provider;

[TestFixture]
public sealed class FilePortRangeStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Join(Path.GetTempPath(), $"agent-up-port-store-{Guid.NewGuid():N}");
        _path = Path.Join(_directory, "nested", "ports.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void Load_returns_null_when_the_store_does_not_exist()
        => Assert.That(new FilePortRangeStore(_path).Load(), Is.Null);

    [Test]
    public async Task SaveAsync_creates_the_parent_directory_and_round_trips_all_state()
    {
        var store = new FilePortRangeStore(_path);
        var expected = new PortRangeData(4, new Dictionary<string, int> { ["alpha"] = 2 }, [0, 1]);

        await store.SaveAsync(expected);

        var loaded = store.Load();
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.HighWaterMark, Is.EqualTo(4));
            Assert.That(loaded.Ranges, Is.EqualTo(expected.Ranges));
            Assert.That(loaded.FreeRanges, Is.EqualTo(expected.FreeRanges));
        });
        Assert.That(File.Exists(_path + ".tmp"), Is.False);
    }

    [Test]
    public async Task SaveAsync_atomically_replaces_an_existing_store()
    {
        var store = new FilePortRangeStore(_path);
        await store.SaveAsync(new PortRangeData(1, new Dictionary<string, int> { ["old"] = 0 }));
        var replacement = new PortRangeData(3, new Dictionary<string, int> { ["new"] = 2 }, [1]);

        await store.SaveAsync(replacement);

        var loaded = store.Load();
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.HighWaterMark, Is.EqualTo(3));
            Assert.That(loaded.Ranges, Is.EqualTo(replacement.Ranges));
            Assert.That(loaded.FreeRanges, Is.EqualTo(replacement.FreeRanges));
        });
    }
}
