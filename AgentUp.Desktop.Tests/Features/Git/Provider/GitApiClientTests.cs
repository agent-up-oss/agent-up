using System.Net;
using System.Diagnostics.CodeAnalysis;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitApiClientTests
{
    [Test]
    public async Task GetChangesAsync_requestsTheWorkspaceScopedChangesRoute()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"workspaceId":"ws 1","branch":"main","fileCount":0,"root":{"name":"","path":"","directories":[],"files":[]}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var tree = await client.GetChangesAsync("ws 1");

        Assert.That(tree!.Branch, Is.EqualTo("main"));
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws%201/git/changes"));
    }

    [Test]
    public async Task GetChangesAsync_returnsNullWhenTheWorkspaceIsUnknown()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        Assert.That(await client.GetChangesAsync("ws-1"), Is.Null);
    }

    [Test]
    public async Task GetCommitQueueAsync_readsProposalAncestryAndGeneration()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"entries":[{"slice":"Commits","message":"feat(Commits): queue","files":["a.cs"],"id":"entry-1","parentCommit":"base","proposalCommit":"tip","state":"ready"}],"unassignedFiles":[],"queueWorktreePath":"/managed/queue","baseCommit":"base","tipCommit":"tip","generation":3}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var queue = await client.GetCommitQueueAsync("ws 1");

        Assert.Multiple(() =>
        {
            Assert.That(queue!.Entries.Single().State, Is.EqualTo("ready"));
            Assert.That(queue.Generation, Is.EqualTo(3));
            Assert.That(queue.QueueWorktreePath, Is.EqualTo("/managed/queue"));
            Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws%201/commit-queue"));
        });
    }

    [Test]
    public async Task GetCommitQueueAsync_returnsNullWhenTheWorkspaceIsUnknown()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        Assert.That(await client.GetCommitQueueAsync("ws-1"), Is.Null);
    }

    [Test]
    public async Task GetCommitQueueAsync_readsAnEmptyQueueFromTheWorkspaceFakeHandler()
    {
        using var handler = new FakeHttpMessageHandler([DesktopDomain.Workspace().Build()]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var client = new GitApiClient(http);

        var queue = await client.GetCommitQueueAsync("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Not.Null);
            Assert.That(queue!.Entries, Is.Empty);
            Assert.That(queue.Generation, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetFileDiffAsync_escapesThePathQueryParameter()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"path":"src/a b.cs","status":"Modified","isBinary":false,"diff":"@@"}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var diff = await client.GetFileDiffAsync("ws-1", "src/a b.cs");

        Assert.That(diff!.Diff, Is.EqualTo("@@"));
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/file?path=src%2Fa%20b.cs"));
    }

    [Test]
    public async Task CommitAsync_surfacesProblemDetailsAsAFailedResult()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.BadRequest,
            """{"detail":"Commit message is required."}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.CommitAsync("ws-1", new GitCommitRequestDto(["a.cs"], " "));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("Commit message is required."));
    }

    [Test]
    public async Task CommitAsync_reportsAnUnknownWorkspaceWithoutThrowing()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.CommitAsync("ws-1", new GitCommitRequestDto(["a.cs"], "chore: touch"));

        Assert.That(result.Found, Is.False);
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task CommitAsync_postsTheSelectedFilesAndMessage()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"commit":"0123456789abcdef","error":null}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.CommitAsync("ws-1", new GitCommitRequestDto(["a.cs"], "chore: touch"));

        Assert.That(result.Commit, Is.EqualTo("0123456789abcdef"));
        Assert.That(handler.LastRequestBody, Does.Contain("chore: touch"));
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/commit"));
    }

    [Test]
    public async Task DiscardAsync_postsTheSelectedFiles()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.DiscardAsync("ws-1", new GitFilesRequestDto(["a.cs"]));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/discard"));
        Assert.That(handler.LastRequestBody, Does.Contain("a.cs"));
    }

    [Test]
    public async Task SwitchBranchAsync_postsTheBranchName()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.SwitchBranchAsync("ws-1", new GitBranchRequestDto("topic", true));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/branch"));
        Assert.That(handler.LastRequestBody, Does.Contain("topic"));
    }

    [Test]
    public async Task CheckoutRemoteAsync_postsTheBranchName()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.CheckoutRemoteAsync("ws-1", new GitCheckoutRequestDto("origin/topic"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/checkout"));
        Assert.That(handler.LastRequestBody, Does.Contain("origin/topic"));
    }

    [Test]
    public async Task FetchAsync_postsTheRemoteName()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null,"head":{"branch":"main","localBranches":["main"]}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.FetchAsync("ws-1", new GitFetchRequestDto("origin"));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/fetch"));
    }

    [Test]
    public async Task PullAsync_postsTheRebaseFlag()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null,"head":{"branch":"main","localBranches":["main"]}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.PullAsync("ws-1", new GitPullRequestDto(true));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/pull"));
        Assert.That(handler.LastRequestBody, Does.Contain("rebase"));
    }

    [Test]
    public async Task PushAsync_postsForceWithLease()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"found":true,"succeeded":true,"error":null,"head":{"branch":"main","localBranches":["main"]}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.PushAsync("ws-1", new GitPushRequestDto(true, false));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/push"));
        Assert.That(handler.LastRequestBody, Does.Contain("forceWithLease"));
    }

    [Test]
    public async Task GetLogAsync_requestsTheBoundedLogRoute()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK,
            """{"commits":[{"id":"abc","shortId":"abc","parents":[],"subject":"initial","author":"A","timestamp":"2026-01-01T00:00:00Z","refs":["main"]}]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var log = await client.GetLogAsync("ws-1", 20);

        Assert.That(log!.Commits[0].Subject, Is.EqualTo("initial"));
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws-1/git/log?max=20"));
    }

    [Test]
    public async Task GetLogAsync_returnsNullWhenTheWorkspaceIsUnknown()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        Assert.That(await client.GetLogAsync("ws-1"), Is.Null);
    }

    [Test]
    public async Task FetchAsync_returnsNotRegisteredWhenTheWorkspaceIsUnknown()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.FetchAsync("ws-1", new GitFetchRequestDto("origin"));

        Assert.That(result.Found, Is.False);
        Assert.That(result.Error, Does.Contain("no longer registered"));
    }

    [Test]
    public async Task PushAsync_treatsAnEmptyBodyAsAFailedResult()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK, "null");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.PushAsync("ws-1", new GitPushRequestDto(false, false));

        Assert.That(result.Found, Is.True);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("empty Git result"));
    }

    [Test]
    public async Task SwitchBranchAsync_returnsNotRegisteredWhenTheWorkspaceIsUnknown()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.NotFound, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.SwitchBranchAsync("ws-1", new GitBranchRequestDto("topic", false));

        Assert.That(result.Found, Is.False);
        Assert.That(result.Error, Does.Contain("no longer registered"));
    }

    [Test]
    public async Task DiscardAsync_treatsAnEmptyBodyAsAFailedResult()
    {
        using var handler = new RecordingGitHandler(HttpStatusCode.OK, "null");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new GitApiClient(http);

        var result = await client.DiscardAsync("ws-1", new GitFilesRequestDto(["a.cs"]));

        Assert.That(result.Found, Is.True);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("empty Git result"));
    }
}

internal sealed class RecordingGitHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public Uri? LastUri { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastUri = request.RequestUri;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(ct);

        return Respond(status, body);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
