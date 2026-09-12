using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Git.HTTP;

[TestFixture]
public sealed class GitChangesHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string _dataDirectory = null!;
    private string _repository = null!;
    // The root factory owns the host; disposing it also disposes the derived factory that
    // WithWebHostBuilder returns, so it has to outlive SetUp rather than be scoped to it.
    private WebApplicationFactory<Program> _rootFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dataDirectory = Path.Join(TestContext.CurrentContext.WorkDirectory, $"agent-up-git-{Guid.NewGuid():N}");
        _repository = Path.Join(_dataDirectory, "widgets");
        await TestGitRepository.InitializeAsync(_repository);
        Directory.CreateDirectory(Path.Join(_repository, "src"));
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// main\n");
        await TestGitRepository.CommitAllAsync(_repository, "initial");
        _rootFactory = new WebApplicationFactory<Program>();
        _factory = _rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", _dataDirectory);
            // The Server requires a bearer token unless authentication is disabled, and these
            // tests exercise the endpoints themselves rather than the authentication handler.
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
    }

    [TearDown]
    public void TearDown()
    {
        _rootFactory.Dispose();
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }

    [Test]
    public async Task Changes_returnsNotFoundForAnUnknownWorkspace()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/workspaces/missing/git/changes");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Changes_returnsTheDirectoryTreeForTheWorkspaceWorktree()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);

        Assert.That(tree!.FileCount, Is.EqualTo(1));
        Assert.That(tree.Root.Directories[0].Name, Is.EqualTo("src"));
        Assert.That(tree.Root.Directories[0].Files[0].Path, Is.EqualTo("src/main.cs"));
        Assert.That(tree.Root.Directories[0].Files[0].Status, Is.EqualTo(GitChangeStatus.Modified));
    }

    [Test]
    public async Task File_returnsTheDiffForAChangedFile()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        var diff = await client.GetFromJsonAsync<GitFileDiff>(
            $"/api/workspaces/{workspaceId}/git/file?path=src%2Fmain.cs", Json);

        Assert.That(diff!.Diff, Does.Contain("+// changed"));
    }

    [Test]
    public async Task File_returnsNotFoundForPathsOutsideTheRepository()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.GetAsync($"/api/workspaces/{workspaceId}/git/file?path=..%2F..%2Fetc%2Fpasswd");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Commit_commitsOnlyTheSelectedFiles()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");
        await File.WriteAllTextAsync(Path.Join(_repository, "NOTES.md"), "notes\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new GitCommitRequest(["src/main.cs"], "fix(App): change main"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<GitCommitResult>(Json);
        Assert.That(result!.Commit, Has.Length.EqualTo(40));

        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);
        Assert.That(tree!.Root.Files.Select(file => file.Path), Is.EqualTo(new[] { "NOTES.md" }));
    }

    [Test]
    public async Task Commit_returnsBadRequestForAnEmptyCommitMessage()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new GitCommitRequest(["src/main.cs"], "   "));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Commit message"));
    }

    [Test]
    public async Task Commit_returnsNotFoundForAnUnknownWorkspace()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/workspaces/missing/git/commit",
            new GitCommitRequest(["src/main.cs"], "fix(App): change main"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetChangesAsync_reportsTheLiveBranchList()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);

        Assert.That(tree!.Branch, Is.EqualTo("main"));
        Assert.That(tree.LocalBranches, Does.Contain("main"));
    }

    [Test]
    public async Task GetHead_returnsTheLiveBranchList()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var head = await client.GetFromJsonAsync<GitHeadState>($"/api/workspaces/{workspaceId}/git/head", Json);

        Assert.That(head!.Branch, Is.EqualTo("main"));
        Assert.That(head.LocalBranches, Does.Contain("main"));
    }

    [Test]
    public async Task Discard_restoresTheSelectedTrackedFile()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/discard",
            new GitFilesRequest(["src/main.cs"]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await File.ReadAllTextAsync(Path.Join(_repository, "src", "main.cs")), Is.EqualTo("// main\n"));
    }

    [Test]
    public async Task Branch_createsAndSwitchesToTheRequestedBranch()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/branch",
            new GitBranchRequest("topic", true));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);
        Assert.That(tree!.Branch, Is.EqualTo("topic"));
    }

    [Test]
    public async Task Branch_returnsBadRequestForAnUnsafeName()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/branch",
            new GitBranchRequest("-c", false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private async Task<string> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/workspaces", new RegisterWorkspaceRequest(
            DisplayName: "widgets",
            RepositoryPath: _repository,
            WorktreePath: _repository,
            Branch: "main",
            Commit: "abc123"));
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<Workspace>(Json);
        return workspace!.Id;
    }
}
