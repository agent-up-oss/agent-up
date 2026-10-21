using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Processes.Repository;

[TestFixture]
public class FileOutputRepositoryTests
{
    private string _tempRoot = "";

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public async Task AppendAsync_storesUntrustedIdentifiersUnderOutputRoot()
    {
        var repository = new FileOutputRepository(_tempRoot);

        await repository.AppendAsync("../outside", "api/../../escape", Line("started"));

        var outputRoot = Path.Join(_tempRoot, "output");
        var files = Directory.GetFiles(outputRoot, "*.log", SearchOption.AllDirectories);

        Assert.That(files, Has.Length.EqualTo(1));
        Assert.That(Path.GetFullPath(files[0]), Does.StartWith(Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar));
        Assert.That(File.ReadAllText(files[0]), Does.Contain("started"));
    }

    [Test]
    public async Task GetAsync_readsLogForSameUntrustedIdentifiers()
    {
        var repository = new FileOutputRepository(_tempRoot);

        await repository.AppendAsync("../outside", "api/../../escape", Line("started"));

        var lines = await repository.GetAsync("../outside", "api/../../escape");

        Assert.That(lines.Texts(), Is.EqualTo(new[] { "started" }));
    }

    [Test]
    public async Task ClearAsync_deletesOnlyTheOwnedLogFile()
    {
        var repository = new FileOutputRepository(_tempRoot);
        var outsideFile = Path.Join(_tempRoot, "escape.log");
        await File.WriteAllTextAsync(outsideFile, "keep");
        await repository.AppendAsync("../escape", "app", Line("delete"));

        await repository.ClearAsync("../escape", "app");

        Assert.That(await repository.GetAsync("../escape", "app"), Is.Empty);
        Assert.That(await File.ReadAllTextAsync(outsideFile), Is.EqualTo("keep"));
    }

    [Test]
    public async Task AppendAsync_preservesStreamIdentityAcrossReload()
    {
        var first = new FileOutputRepository(_tempRoot);
        await first.AppendAsync("ws", "Web", new ProcessOutputLine("out", ProcessOutputStream.Stdout));
        await first.AppendAsync("ws", "Web", new ProcessOutputLine("[err] boom", ProcessOutputStream.Stderr));

        var reloaded = new FileOutputRepository(_tempRoot);
        var lines = await reloaded.GetAsync("ws", "Web");

        Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { "out", "[err] boom" }));
        Assert.That(lines.Select(line => line.Stream), Is.EqualTo(new[] { ProcessOutputStream.Stdout, ProcessOutputStream.Stderr }));
    }

    [Test]
    public async Task AppendAsync_overflowProducesExplicitTruncationMarker()
    {
        var repository = new FileOutputRepository(_tempRoot, capacity: 3);
        await repository.AppendAsync("ws", "Web", Line("a"));
        await repository.AppendAsync("ws", "Web", Line("b"));
        await repository.AppendAsync("ws", "Web", Line("c"));
        await repository.AppendAsync("ws", "Web", Line("d"));

        var lines = (await repository.GetAsync("ws", "Web")).Texts();

        Assert.That(lines[0], Is.EqualTo(ProcessOutputBuffer.TruncationMarker(2)));
        Assert.That(lines.Skip(1), Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public async Task AppendAsync_serialisesConcurrentWritesWithoutSplicing()
    {
        var repository = new FileOutputRepository(_tempRoot);
        var tasks = Enumerable.Range(0, 200)
            .Select(index => repository.AppendAsync("ws", "Web", Line($"line-{index:000}")));

        await Task.WhenAll(tasks);

        var lines = (await repository.GetAsync("ws", "Web")).Texts();
        Assert.That(lines, Has.Count.EqualTo(200));
        Assert.That(lines, Has.All.Match("line-\\d{3}"));
        Assert.That(lines.Distinct().Count(), Is.EqualTo(200));
    }

    private static ProcessOutputLine Line(string text)
        => new(text, ProcessOutputStream.Stdout);
}
