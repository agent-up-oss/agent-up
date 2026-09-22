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
    public async Task LoadAsync_forwardsNamedRuntimeSections()
    {
        await File.WriteAllTextAsync(Path.Join(_temporaryDirectory, "agent-up.json"),
            """{"name":"App","python":[{"name":"api","script":"main.py"}],"dotnet":[{"name":"Api","run":{"project":"Api.csproj"}}]}""");

        var result = await new WorkspaceConfigurationProvider().LoadAsync(_temporaryDirectory);

        var sections = result.Configuration!.RuntimeSections!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(sections.Select(section => section.ModuleId), Is.EquivalentTo(new[] { "python", "dotnet" }));
            Assert.That(sections.Single(section => section.ModuleId == "python").Items.Single().Parameters!["script"], Is.EqualTo("main.py"));
            Assert.That(result.Configuration.Dotnet!.Single().Run.Project, Is.EqualTo("Api.csproj"));
        });
    }

    [Test]
    public async Task LoadAsync_keepsLegacyApplicationCollections()
    {
        await File.WriteAllTextAsync(Path.Join(_temporaryDirectory, "agent-up.json"),
            """{"name":"App","applications":[{"name":"web","command":"npm start"}],"desktopApplications":[{"name":"editor","command":"code ."}],"services":[{"name":"db","image":"postgres:16"}]}""");

        var result = await new WorkspaceConfigurationProvider().LoadAsync(_temporaryDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Configuration!.Applications!.Single().Name, Is.EqualTo("web"));
            Assert.That(result.Configuration.DesktopApplications!.Single().Name, Is.EqualTo("editor"));
            Assert.That(result.Configuration.Services!.Single().Name, Is.EqualTo("db"));
            Assert.That(result.Configuration.RuntimeSections, Is.Empty);
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
