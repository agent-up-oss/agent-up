using AgentUp.Server.Features.Processes.Repositories;

namespace AgentUp.Server.Tests.Features.Processes.Repository;

[TestFixture]
public class FileOutputRepositoryTests
{
    private string _tempRoot = "";

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
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

        await repository.AppendAsync("../outside", "api/../../escape", "started");

        var outputRoot = Path.Combine(_tempRoot, "output");
        var files = Directory.GetFiles(outputRoot, "*.log", SearchOption.AllDirectories);

        Assert.That(files, Has.Length.EqualTo(1));
        Assert.That(Path.GetFullPath(files[0]), Does.StartWith(Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar));
        Assert.That(File.ReadAllLines(files[0]), Is.EqualTo(new[] { "started" }));
    }

    [Test]
    public async Task GetAsync_readsLogForSameUntrustedIdentifiers()
    {
        var repository = new FileOutputRepository(_tempRoot);

        await repository.AppendAsync("../outside", "api/../../escape", "started");

        var lines = await repository.GetAsync("../outside", "api/../../escape");

        Assert.That(lines, Is.EqualTo(new[] { "started" }));
    }

    [Test]
    public async Task ClearAsync_deletesOnlyTheOwnedLogFile()
    {
        var repository = new FileOutputRepository(_tempRoot);
        var outsideFile = Path.Combine(_tempRoot, "escape.log");
        await File.WriteAllTextAsync(outsideFile, "keep");
        await repository.AppendAsync("../escape", "app", "delete");

        await repository.ClearAsync("../escape", "app");

        Assert.That(await repository.GetAsync("../escape", "app"), Is.Empty);
        Assert.That(await File.ReadAllTextAsync(outsideFile), Is.EqualTo("keep"));
    }
}
