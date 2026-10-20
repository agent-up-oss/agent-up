using System.Net;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceApiClientReviewSnapshotTests
{
    [Test]
    public async Task GetReviewSnapshotAsync_ReturnsJsonForKnownWorkspace()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"workspaceId":"w1"}""")
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var result = await client.GetReviewSnapshotAsync("w1");

        Assert.That(result, Does.Contain("w1"));
    }

    [Test]
    public async Task GetReviewSnapshotAsync_ReturnsNullForMissingWorkspace()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        Assert.That(await client.GetReviewSnapshotAsync("missing"), Is.Null);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
