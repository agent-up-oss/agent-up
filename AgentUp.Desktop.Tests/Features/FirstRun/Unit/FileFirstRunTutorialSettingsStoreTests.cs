using AgentUp.Desktop.Features.FirstRun.Services;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Unit;

[TestFixture]
public class FileFirstRunTutorialSettingsStoreTests
{
    private string _testRoot = "";

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"agent-up-first-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public async Task LoadAsync_returnsDefaultSettings_whenFileDoesNotExist()
    {
        var store = new FileFirstRunTutorialSettingsStore(Path.Combine(_testRoot, "settings.json"));

        var settings = await store.LoadAsync();

        Assert.That(settings.TutorialCompleted, Is.False);
        Assert.That(settings.TutorialSkipped, Is.False);
        Assert.That(settings.CompletedStep, Is.EqualTo(0));
    }

    [Test]
    public async Task LoadAsync_returnsDefaultSettings_whenFileIsInvalidJson()
    {
        var path = Path.Combine(_testRoot, "settings.json");
        await File.WriteAllTextAsync(path, "{not json");
        var store = new FileFirstRunTutorialSettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.That(settings.TutorialCompleted, Is.False);
        Assert.That(settings.TutorialSkipped, Is.False);
        Assert.That(settings.CompletedStep, Is.EqualTo(0));
    }

    [Test]
    public async Task SaveAsync_persistsTutorialSettings()
    {
        var path = Path.Combine(_testRoot, "nested", "settings.json");
        var store = new FileFirstRunTutorialSettingsStore(path);

        await store.SaveAsync(new FirstRunTutorialSettings(true, false, 2));
        var settings = await store.LoadAsync();

        Assert.That(settings.TutorialCompleted, Is.True);
        Assert.That(settings.TutorialSkipped, Is.False);
        Assert.That(settings.CompletedStep, Is.EqualTo(2));
    }
}
