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
    public async Task CommitQueue_returnsNotFoundForAnUnknownWorkspace()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/workspaces/missing/commit-queue");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CommitQueue_returnsServerOwnedQueueForDesktopAndMobileClients()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var queue = await client.GetFromJsonAsync<AgentUp.Server.Features.Commits.DTOs.CommitsStatusResult>(
            $"/api/workspaces/{workspaceId}/commit-queue", Json);

        Assert.That(queue, Is.Not.Null);
        Assert.That(queue!.Entries, Is.Empty);
        Assert.That(queue.Generation, Is.Zero);
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
        var workspace = await client.GetFromJsonAsync<Workspace>($"/api/workspaces/{workspaceId}", Json);
        Assert.That(workspace!.Branch, Is.EqualTo("topic"));
    }

    [Test]
    public async Task Checkout_createsALocalBranchFromTheRemote()
    {
        var origin = Path.Join(_dataDirectory, "origin");
        await TestGitRepository.InitializeAsync(origin);
        await File.WriteAllTextAsync(Path.Join(origin, "ORIGIN.md"), "origin\n");
        await TestGitRepository.CommitAllAsync(origin, "origin");
        await TestGitRepository.RunAsync(origin, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(origin, "TOPIC.md"), "topic\n");
        await TestGitRepository.CommitAllAsync(origin, "topic");
        await TestGitRepository.RunAsync(origin, "switch", "main");
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(_repository, "fetch", "origin");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/checkout",
            new GitCheckoutRequest("topic"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var head = await client.GetFromJsonAsync<GitHeadState>($"/api/workspaces/{workspaceId}/git/head", Json);
        Assert.That(head!.Branch, Is.EqualTo("topic"));
        Assert.That(head.RemoteBranches.Select(branch => branch.Name), Does.Contain("topic"));
    }

    [Test]
    public async Task Log_returnsDecoratedHistory()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var log = await client.GetFromJsonAsync<GitLog>($"/api/workspaces/{workspaceId}/git/log?max=10", Json);

        Assert.That(log!.Commits, Is.Not.Empty);
        Assert.That(log.Commits[0].Subject, Is.EqualTo("initial"));
    }

    [Test]
    public async Task Log_pagesOlderHistoryFromSkipAndUntil()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "second.md"), "second\n");
        await TestGitRepository.CommitAllAsync(_repository, "second");
        await File.WriteAllTextAsync(Path.Join(_repository, "third.md"), "third\n");
        await TestGitRepository.CommitAllAsync(_repository, "third");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var newest = await client.GetFromJsonAsync<GitLog>($"/api/workspaces/{workspaceId}/git/log?max=1", Json);
        var skipped = await client.GetFromJsonAsync<GitLog>(
            $"/api/workspaces/{workspaceId}/git/log?max=1&skip=1", Json);
        var older = await client.GetFromJsonAsync<GitLog>(
            $"/api/workspaces/{workspaceId}/git/log?max=1&until={newest!.Commits[0].Id}", Json);
        var last = await client.GetFromJsonAsync<GitLog>(
            $"/api/workspaces/{workspaceId}/git/log?max=1&skip=2", Json);

        Assert.That(newest.Commits[0].Subject, Is.EqualTo("third"));
        Assert.That(newest.HasMore, Is.True);
        Assert.That(skipped!.Commits[0].Subject, Is.EqualTo("second"));
        Assert.That(older!.Commits[0].Subject, Is.EqualTo("second"));
        Assert.That(last!.Commits[0].Subject, Is.EqualTo("initial"));
        Assert.That(last.HasMore, Is.False);
    }

    [Test]
    public async Task Fetch_updatesRemoteTrackingBranches()
    {
        var origin = Path.Join(_dataDirectory, "origin-fetch");
        await TestGitRepository.InitializeAsync(origin);
        await File.WriteAllTextAsync(Path.Join(origin, "ORIGIN.md"), "origin\n");
        await TestGitRepository.CommitAllAsync(origin, "origin");
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/fetch",
            new GitFetchRequest("origin"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<GitSyncResult>(Json);
        Assert.That(result!.Succeeded, Is.True);
        Assert.That(result.Head!.RemoteBranches.Select(branch => branch.Name), Does.Contain("main"));
    }

    [Test]
    public async Task Pull_fastForwardsFromTheRemote()
    {
        var origin = Path.Join(_dataDirectory, "origin-pull");
        await TestGitRepository.RunAsync(_dataDirectory, "clone", _repository, origin);
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(_repository, "fetch", "origin");
        await TestGitRepository.RunAsync(_repository, "branch", "--set-upstream-to", "origin/main", "main");
        await File.WriteAllTextAsync(Path.Join(origin, "later.md"), "later\n");
        await TestGitRepository.CommitAllAsync(origin, "later");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/pull",
            new GitPullRequest(false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<GitSyncResult>(Json);
        Assert.That(result!.Succeeded, Is.True);
        Assert.That(File.Exists(Path.Join(_repository, "later.md")), Is.True);
    }

    [Test]
    public async Task Push_publishesTheCurrentBranch()
    {
        var origin = Path.Join(_dataDirectory, "origin-push.git");
        await TestGitRepository.RunAsync(_dataDirectory, "clone", "--bare", _repository, origin);
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(_repository, "fetch", "origin");
        await TestGitRepository.RunAsync(_repository, "branch", "--set-upstream-to", "origin/main", "main");
        await File.WriteAllTextAsync(Path.Join(_repository, "from-workspace.md"), "push\n");
        await TestGitRepository.CommitAllAsync(_repository, "from workspace");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/push",
            new GitPushRequest(false, false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<GitSyncResult>(Json);
        Assert.That(result!.Succeeded, Is.True);
        Assert.That(await TestGitRepository.ReadAsync(origin, "log", "-1", "--pretty=%s"), Is.EqualTo("from workspace"));
    }

    [Test]
    public async Task Commit_acceptsTheMobileClientPayload()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new { files = new[] { "src/main.cs" }, message = "fix(App): change main" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<GitCommitResult>(Json);
        Assert.That(result!.Succeeded, Is.True);
        Assert.That(result.Commit, Has.Length.EqualTo(40));
    }

    [Test]
    public async Task Commit_returnsAStructuredErrorWhenGitIdentityIsMissing()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");
        await TestGitRepository.UnsetIdentityAsync(_repository);

        using var isolation = TestGitConfigIsolation.Begin();
        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new { files = new[] { "src/main.cs" }, message = "fix(App): change main" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("user.name"));
        Assert.That(body, Does.Not.Contain("Please tell me who you are"));
        Assert.That(body, Does.Not.Contain("***"));
    }

    [Test]
    public async Task Commit_returnsBadRequestWhenADirectoryPathIsSelected()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// changed\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new { files = new[] { "src" }, message = "chore: sweep" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("not a changed file"));
    }

    [Test]
    public async Task Commit_commitsUntrackedFilesFromTheMobilePayload()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        Directory.CreateDirectory(Path.Join(_repository, "docs"));
        await File.WriteAllTextAsync(Path.Join(_repository, "docs", "notes.md"), "notes\n");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/commit",
            new { files = new[] { "docs/notes.md" }, message = "docs: add notes" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);
        Assert.That(tree!.FileCount, Is.Zero);
    }

    [Test]
    public async Task Changes_returnsAnEmptyTreeWhenTheWorktreeIsClean()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);

        Assert.That(tree!.FileCount, Is.Zero);
        Assert.That(tree.Root.Files, Is.Empty);
        Assert.That(tree.Root.Directories, Is.Empty);
    }

    [Test]
    public async Task Changes_reportsConflictedFiles()
    {
        await File.WriteAllTextAsync(Path.Join(_repository, "conflict.txt"), "base\n");
        await TestGitRepository.CommitAllAsync(_repository, "conflict base");
        await TestGitRepository.RunAsync(_repository, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(_repository, "conflict.txt"), "topic\n");
        await TestGitRepository.CommitAllAsync(_repository, "topic");
        await TestGitRepository.RunAsync(_repository, "switch", "main");
        await File.WriteAllTextAsync(Path.Join(_repository, "conflict.txt"), "main\n");
        await TestGitRepository.CommitAllAsync(_repository, "main");
        await TestGitRepository.RunAsync(_repository, ["merge", "topic"], [1]);
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var tree = await client.GetFromJsonAsync<GitChangeTree>($"/api/workspaces/{workspaceId}/git/changes", Json);

        Assert.That(tree!.Root.Files.Single(file => file.Path == "conflict.txt").Status, Is.EqualTo(GitChangeStatus.Conflicted));
    }

    [Test]
    public async Task File_marksABinaryDiff()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllBytesAsync(Path.Join(_repository, "blob.bin"), [0x00, 0x01, 0xff]);

        var diff = await client.GetFromJsonAsync<GitFileDiff>(
            $"/api/workspaces/{workspaceId}/git/file?path=blob.bin", Json);

        Assert.That(diff!.IsBinary, Is.True);
    }

    [Test]
    public async Task Branch_returnsAStructuredErrorWhenTheWorktreeIsDirty()
    {
        await TestGitRepository.RunAsync(_repository, "switch", "-c", "topic");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// topic\n");
        await TestGitRepository.CommitAllAsync(_repository, "topic");
        await TestGitRepository.RunAsync(_repository, "switch", "main");
        await File.WriteAllTextAsync(Path.Join(_repository, "src", "main.cs"), "// dirty\n");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/branch",
            new GitBranchRequest("topic", false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("conflicting local changes"));
    }

    [Test]
    public async Task Pull_returnsAStructuredErrorWhenHistoriesDiverge()
    {
        var origin = Path.Join(_dataDirectory, "origin-diverge");
        await TestGitRepository.RunAsync(_dataDirectory, "clone", _repository, origin);
        await TestGitRepository.ConfigureIdentityAsync(origin);
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        await TestGitRepository.RunAsync(_repository, "fetch", "origin");
        await TestGitRepository.RunAsync(_repository, "branch", "--set-upstream-to", "origin/main", "main");
        await File.WriteAllTextAsync(Path.Join(origin, "later.md"), "later\n");
        await TestGitRepository.CommitAllAsync(origin, "later");
        await File.WriteAllTextAsync(Path.Join(_repository, "local.md"), "local\n");
        await TestGitRepository.CommitAllAsync(_repository, "local");
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/pull",
            new GitPullRequest(false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("fast-forward"));
        Assert.That(body, Does.Not.Contain("Not possible to fast-forward"));
    }

    [Test]
    public async Task Push_returnsAStructuredErrorWhenTheBranchHasNoUpstream()
    {
        var origin = Path.Join(_dataDirectory, "origin-noupstream.git");
        await TestGitRepository.RunAsync(_dataDirectory, "clone", "--bare", _repository, origin);
        await TestGitRepository.RunAsync(_repository, "remote", "add", "origin", origin);
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);
        await File.WriteAllTextAsync(Path.Join(_repository, "later.md"), "later\n");
        await TestGitRepository.CommitAllAsync(_repository, "later");

        using var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/git/push",
            new GitPushRequest(false, false));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("no upstream"));
    }

    [Test]
    public async Task Log_clampsMaxToTwoHundred()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAsync(client);

        var log = await client.GetFromJsonAsync<GitLog>($"/api/workspaces/{workspaceId}/git/log?max=500", Json);

        Assert.That(log!.Commits, Has.Count.EqualTo(1));
        Assert.That(log.HasMore, Is.False);
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
