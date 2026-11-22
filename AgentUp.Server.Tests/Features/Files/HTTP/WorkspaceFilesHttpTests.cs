using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Files.HTTP;

[TestFixture]
public sealed class WorkspaceFilesHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly List<IDisposable> _hosts = [];
    private readonly List<string> _folders = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _hosts)
            host.Dispose();
        foreach (var folder in _folders.Where(Directory.Exists))
            Directory.Delete(folder, recursive: true);
    }

    [Test]
    public async Task Directory_isNotFoundForAnUnknownWorkspace()
    {
        using var client = await HostAsync(withRepository: false);

        using var response = await client.GetAsync("/api/workspaces/missing/files/directory");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Directory_listsTheTopLevelWithGitMarkers()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);
        await File.WriteAllTextAsync(Path.Join(Repository, "src", "main.cs"), "// changed\n");
        await File.WriteAllTextAsync(Path.Join(Repository, "src", "added.cs"), "// added\n");

        var listing = await client.GetFromJsonAsync<WorkspaceDirectoryListing>($"/api/workspaces/{workspaceId}/files/directory", Json);

        Assert.That(listing!.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { "src", "README.md" }));
        Assert.That(listing.Entries[0].ChangedFiles, Is.EqualTo(2));
    }

    [Test]
    public async Task Directory_opensAFolderOnDemand()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);
        await File.WriteAllTextAsync(Path.Join(Repository, "src", "added.cs"), "// added\n");

        var listing = await client.GetFromJsonAsync<WorkspaceDirectoryListing>($"/api/workspaces/{workspaceId}/files/directory?path=src", Json);

        Assert.That(listing!.Entries.Single(entry => entry.Name == "added.cs").Status, Is.EqualTo(GitChangeStatus.Untracked));
        Assert.That(listing.Entries.Single(entry => entry.Name == "main.cs").Status, Is.Null);
    }

    [Test]
    public async Task Directory_mapsGitStatusForAWorkspaceInsideItsRepository()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Path.Join(Repository, "src"));
        await File.WriteAllTextAsync(Path.Join(Repository, "src", "main.cs"), "// changed\n");

        var listing = await client.GetFromJsonAsync<WorkspaceDirectoryListing>($"/api/workspaces/{workspaceId}/files/directory", Json);

        Assert.That(listing!.Entries.Single().Path, Is.EqualTo("main.cs"));
        Assert.That(listing.Entries.Single().Status, Is.EqualTo(GitChangeStatus.Modified));
    }

    [Test]
    public async Task Directory_worksForAFolderThatIsNotARepository()
    {
        using var client = await HostAsync(withRepository: false);
        var folder = Directory.CreateTempSubdirectory("agent-up-plain-files").FullName;
        _folders.Add(folder);
        await File.WriteAllTextAsync(Path.Join(folder, "notes.txt"), "plain\n");
        var workspaceId = await RegisterAsync(client, folder);

        var listing = await client.GetFromJsonAsync<WorkspaceDirectoryListing>($"/api/workspaces/{workspaceId}/files/directory", Json);

        Assert.That(listing!.Entries.Single().Name, Is.EqualTo("notes.txt"));
        Assert.That(listing.Entries.Single().Status, Is.Null);
    }

    [Test]
    public async Task Directory_refusesThePathOutsideTheWorkspace()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);

        using var response = await client.GetAsync($"/api/workspaces/{workspaceId}/files/directory?path=..%2F..");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task File_returnsTheWholeFileNotJustTheChange()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);

        var file = await client.GetFromJsonAsync<WorkspaceFileContent>($"/api/workspaces/{workspaceId}/files/file?path=README.md", Json);

        Assert.That(file!.Content, Is.EqualTo("# widgets\n\nA sample.\n"));
        Assert.That(file.Status, Is.Null);
    }

    [Test]
    public async Task PutFile_savesAnEditAndGitSeesIt()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);
        var opened = await client.GetFromJsonAsync<WorkspaceFileContent>($"/api/workspaces/{workspaceId}/files/file?path=README.md", Json);

        using var response = await client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/files/file",
            new WorkspaceFileWriteRequest("README.md", "# widgets\n\nEdited by hand.\n", opened!.Version));

        var saved = await response.Content.ReadFromJsonAsync<WorkspaceFileContent>(Json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(saved!.Status, Is.EqualTo(GitChangeStatus.Modified));
        Assert.That(await File.ReadAllTextAsync(Path.Join(Repository, "README.md")), Does.Contain("Edited by hand."));
    }

    [Test]
    public async Task PutFile_isAConflictWhenTheFileChangedSinceItWasOpened()
    {
        using var client = await HostAsync(withRepository: true);
        var workspaceId = await RegisterAsync(client, Repository);
        var opened = await client.GetFromJsonAsync<WorkspaceFileContent>($"/api/workspaces/{workspaceId}/files/file?path=README.md", Json);
        await File.WriteAllTextAsync(Path.Join(Repository, "README.md"), "# widgets\n\nThe agent's edit.\n");

        using var response = await client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/files/file",
            new WorkspaceFileWriteRequest("README.md", "mine\n", opened!.Version));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await File.ReadAllTextAsync(Path.Join(Repository, "README.md")), Does.Contain("The agent's edit."));
    }

    private string Repository { get; set; } = string.Empty;

    private async Task<HttpClient> HostAsync(bool withRepository)
    {
        var data = Path.Join(TestContext.CurrentContext.WorkDirectory, $"agent-up-files-{Guid.NewGuid():N}");
        _folders.Add(data);
        Directory.CreateDirectory(data);
        if (withRepository)
        {
            Repository = Path.Join(data, "widgets");
            await TestGitRepository.InitializeAsync(Repository);
            Directory.CreateDirectory(Path.Join(Repository, "src"));
            await File.WriteAllTextAsync(Path.Join(Repository, "src", "main.cs"), "// main\n");
            await File.WriteAllTextAsync(Path.Join(Repository, "README.md"), "# widgets\n\nA sample.\n");
            await TestGitRepository.CommitAllAsync(Repository, "initial");
        }

        var root = new WebApplicationFactory<Program>();
        _hosts.Add(root);
        var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", data);
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
        return factory.CreateClient();
    }

    private static async Task<string> RegisterAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace().Named("widgets").At(path).AtCommit("abc123").Build());
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<Workspace>(Json);
        return workspace!.Id;
    }
}
