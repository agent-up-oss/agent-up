using System.Net;
using AgentUp.Desktop.Features.Audit.Providers;

namespace AgentUp.Desktop.Tests.Features.Audit.Provider;

[TestFixture]
public sealed class ApplicationAuditApiClientTests
{
    [Test]
    public async Task GetPageAsync_EncodesIdentityAndCursor()
    {
        Uri? requested = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"items\":[],\"nextBefore\":null,\"nextBeforeEventId\":null}") };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var client = new ApplicationAuditApiClient(http);
        var before = DateTimeOffset.Parse("2026-08-22T12:00:00Z");

        await client.GetPageAsync("workspace one", "web/app", before, "event/one", 50, CancellationToken.None);

        Assert.That(requested!.PathAndQuery, Does.StartWith("/api/audit/workspaces/workspace%20one/applications/web%2Fapp?limit=50&before="));
        Assert.That(requested.PathAndQuery, Does.Contain("&beforeEventId=event%2Fone"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
