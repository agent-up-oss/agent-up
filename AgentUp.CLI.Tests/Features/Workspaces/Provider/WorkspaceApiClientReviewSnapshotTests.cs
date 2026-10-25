using System.Net;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceApiClientReviewSnapshotTests
{
    [Test]
    public async Task GetReviewSnapshotAsync_ReturnsHtmlForKnownWorkspace()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>w1</html>", System.Text.Encoding.UTF8, "text/html")
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var result = await client.GetReviewSnapshotAsync("w1");

        Assert.That(result, Does.Contain("<html>w1</html>"));
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
