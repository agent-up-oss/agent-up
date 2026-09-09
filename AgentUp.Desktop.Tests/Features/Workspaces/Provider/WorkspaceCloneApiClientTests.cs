using System.Net;
using System.Diagnostics.CodeAnalysis;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceCloneApiClientTests
{
    private const string CreatedWorkspace =
        """{"id":"ws-1","displayName":"widgets","repositoryPath":"/clones/widgets","worktreePath":"/clones/widgets","branch":"main","commit":"abc123","state":"Stopped","applications":[]}""";

    [Test]
    public async Task CloneAsync_postsTheRepositoryAndBranchToTheSourceClonesRoute()
    {
        using var handler = new RecordingCloneHandler(HttpStatusCode.Created, CreatedWorkspace);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var workspace = await client.CloneAsync(
            new CloneSourceRequestDto("https://example.test/acme/widgets.git", "main"));

        Assert.That(workspace.Id, Is.EqualTo("ws-1"));
        Assert.That(handler.LastUri!.PathAndQuery, Is.EqualTo("/api/source-clones"));
        Assert.That(handler.LastRequestBody, Does.Contain("https://example.test/acme/widgets.git"));
        Assert.That(handler.LastRequestBody, Does.Contain("main"));
    }

    [Test]
    public void CloneAsync_surfacesTheServerProblemDetail()
    {
        using var handler = new RecordingCloneHandler(
            HttpStatusCode.BadRequest,
            """{"detail":"Branch must be a valid Git branch name."}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.CloneAsync(new CloneSourceRequestDto("https://example.test/acme/widgets.git", "--x")));

        Assert.That(exception!.Message, Is.EqualTo("Branch must be a valid Git branch name."));
    }
}

internal sealed class RecordingCloneHandler(HttpStatusCode status, string body) : HttpMessageHandler
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
