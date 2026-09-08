using System.Net;
using System.Net.Http.Json;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.SourceClones.HTTP;

[TestFixture]
public sealed class SourceClonesHttpTests
{
    private string _dataDirectory = null!;
    private string _clonesRoot = null!;
    private string? _previousRoot;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDirectory = Path.Join(TestContext.CurrentContext.WorkDirectory, $"agent-up-clones-{Guid.NewGuid():N}");
        _clonesRoot = Path.Join(_dataDirectory, "managed-sources");
        _previousRoot = Environment.GetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable);
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, _clonesRoot);
        using var factory = new WebApplicationFactory<Program>();
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Storage:DataDirectory", _dataDirectory));
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(SourceCloneRootProvider.RootEnvironmentVariable, _previousRoot);
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task GetRoot_reportsTheInjectedSourceClonesRoot()
    {
        using var client = _factory.CreateClient();

        var root = await client.GetFromJsonAsync<SourceCloneRoot>("/api/source-clones/root");

        Assert.That(root!.Path, Is.EqualTo(Path.GetFullPath(_clonesRoot)));
    }

    [Test]
    public async Task Post_rejectsRepositoriesThatAreNotRemoteUrls()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/source-clones",
            new CloneSourceRequest("../../etc/passwd", "main"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Repository"));
    }

    [Test]
    public async Task Post_rejectsBranchesThatAreNotValidGitBranchNames()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/source-clones",
            new CloneSourceRequest("https://example.test/acme/widgets.git", "--upload-pack=whoami"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Branch"));
    }

    [Test]
    public async Task Post_rejectsLocalFileRemotesAndRegistersNoWorkspace()
    {
        var origin = Path.Join(_dataDirectory, "origin", "widgets");
        await TestGitRepository.InitializeAsync(origin);
        await File.WriteAllTextAsync(Path.Join(origin, "README.md"), "widgets\n");
        await TestGitRepository.CommitAllAsync(origin, "initial");
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/source-clones",
            new CloneSourceRequest($"file://{origin}", "main"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(Directory.Exists(_clonesRoot), Is.False);

        var workspaces = await client.GetFromJsonAsync<List<Workspace>>("/api/workspaces");
        Assert.That(workspaces, Is.Empty);
    }
}
