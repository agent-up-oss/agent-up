using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Models;
using AgentUp.Server.Features.Ports.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Ports.Unit;

[TestFixture]
public sealed class PortAllocationServiceTests
{
    [Test]
    public async Task GetBasePortAsync_assigns_stable_non_overlapping_ranges()
    {
        var store = new RecordingStore();
        var service = Create(store);

        var first = await service.GetBasePortAsync("first");
        var same = await service.GetBasePortAsync("first");
        var second = await service.GetBasePortAsync("second");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(10000));
            Assert.That(same, Is.EqualTo(first));
            Assert.That(second, Is.EqualTo(10100));
            Assert.That(store.Saves, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ReleaseAsync_recycles_the_released_range_before_allocating_a_new_one()
    {
        var store = new RecordingStore();
        var service = Create(store);
        var released = await service.GetBasePortAsync("released");
        await service.GetBasePortAsync("still-active");

        await service.ReleaseAsync("released");
        var recycled = await service.GetBasePortAsync("replacement");

        Assert.That(recycled, Is.EqualTo(released));
        Assert.That(store.Saves[^1].FreeRanges, Is.Empty);
    }

    [Test]
    public async Task ReleaseAsync_does_not_save_when_the_workspace_is_unknown()
    {
        var store = new RecordingStore();
        var service = Create(store);

        await service.ReleaseAsync("missing");

        Assert.That(store.Saves, Is.Empty);
    }

    [Test]
    public async Task Constructor_restores_assignments_high_water_mark_and_free_ranges()
    {
        var store = new RecordingStore
        {
            Loaded = new PortRangeData(4, new Dictionary<string, int> { ["existing"] = 2 }, [1])
        };
        var service = Create(store);

        var existing = await service.GetBasePortAsync("existing");
        var recycled = await service.GetBasePortAsync("recycled");
        var fresh = await service.GetBasePortAsync("fresh");

        Assert.Multiple(() =>
        {
            Assert.That(existing, Is.EqualTo(10200));
            Assert.That(recycled, Is.EqualTo(10100));
            Assert.That(fresh, Is.EqualTo(10400));
        });
    }

    [Test]
    public async Task GetConflictFreeBasePortAsync_reassigns_after_a_conflict_and_persists_the_winner()
    {
        var store = new RecordingStore();
        var ports = new SequenceAvailability(false, true);
        var service = Create(store, ports);

        var result = await service.GetConflictFreeBasePortAsync("workspace", 3);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(10100));
            Assert.That(ports.Requests, Is.EqualTo(new[] { (10000, 3), (10100, 3) }));
            Assert.That(store.Saves, Has.Count.EqualTo(1));
            Assert.That(store.Saves[0].Ranges["workspace"], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetConflictFreeBasePortAsync_with_no_ports_skips_the_probe()
    {
        var ports = new SequenceAvailability();
        var service = Create(new RecordingStore(), ports);

        var result = await service.GetConflictFreeBasePortAsync("workspace", 0);

        Assert.That(result, Is.EqualTo(10000));
        Assert.That(ports.Requests, Is.Empty);
    }

    [Test]
    public void GetConflictFreeBasePortAsync_fails_after_twenty_conflicted_ranges()
    {
        var ports = new SequenceAvailability(Enumerable.Repeat(false, 20).ToArray());
        var service = Create(new RecordingStore(), ports);

        var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetConflictFreeBasePortAsync("blocked", 1));

        Assert.That(error!.Message, Does.Contain("after 20 attempts"));
        Assert.That(ports.Requests, Has.Count.EqualTo(20));
    }

    [TestCaseSource(nameof(RecoverableLoadFailures))]
    public async Task Constructor_ignores_recoverable_store_failures(Exception failure)
    {
        var service = Create(new RecordingStore { LoadFailure = failure });

        Assert.That(await service.GetBasePortAsync("workspace"), Is.EqualTo(10000));
    }

    private static IEnumerable<Exception> RecoverableLoadFailures()
    {
        yield return new IOException("unavailable");
        yield return new UnauthorizedAccessException("denied");
        yield return new System.Text.Json.JsonException("invalid");
    }

    private static PortAllocationService Create(
        RecordingStore store,
        IPortAvailabilityProvider? ports = null)
        => new(store, ports ?? new SequenceAvailability(true), NullLogger<PortAllocationService>.Instance);

    private sealed class RecordingStore : IPortRangeStore
    {
        public PortRangeData? Loaded { get; init; }
        public Exception? LoadFailure { get; init; }
        public List<PortRangeData> Saves { get; } = [];

        public PortRangeData? Load()
            => LoadFailure is null ? Loaded : throw LoadFailure;

        public Task SaveAsync(PortRangeData data)
        {
            Saves.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceAvailability(params bool[] results) : IPortAvailabilityProvider
    {
        private readonly Queue<bool> _results = new(results);
        public List<(int BasePort, int Count)> Requests { get; } = [];

        public bool ArePortsAvailable(int basePort, int count)
        {
            Requests.Add((basePort, count));
            return _results.Dequeue();
        }
    }
}
