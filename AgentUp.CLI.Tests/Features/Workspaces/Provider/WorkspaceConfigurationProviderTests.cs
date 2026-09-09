using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceConfigurationProviderTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Join(Path.GetTempPath(), "AgentUp-ConfigurationTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_temporaryDirectory, recursive: true);

    [Test]
    public async Task LoadAsync_findsConfigurationInNearestParentDirectory()
    {
        await File.WriteAllTextAsync(Path.Join(_temporaryDirectory, "agent-up.json"), "{\"name\":\"Parent workspace\"}");
        var nestedDirectory = Path.Join(_temporaryDirectory, "src", "application");
        Directory.CreateDirectory(nestedDirectory);

        var result = await new WorkspaceConfigurationProvider().LoadAsync(nestedDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.WorkspaceRoot, Is.EqualTo(_temporaryDirectory));
            Assert.That(result.Configuration!.Name, Is.EqualTo("Parent workspace"));
        });
    }

    [Test]
    public async Task LoadAsync_reportsMissingConfigurationAfterSearchingParents()
    {
        var nestedDirectory = Path.Join(_temporaryDirectory, "src", "application");
        Directory.CreateDirectory(nestedDirectory);

        var result = await new WorkspaceConfigurationProvider().LoadAsync(nestedDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("current directory or any parent directory"));
        });
    }
}
