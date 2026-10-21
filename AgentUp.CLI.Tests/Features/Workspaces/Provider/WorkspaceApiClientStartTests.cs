using System.Collections.Concurrent;
using System.Net;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceApiClientStartTests
{
    [Test]
    public async Task StartWorkspaceAsync_readsDegradedFailuresFromTheJsonBody()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"state":"Degraded","failedApplications":[{"name":"Api","reason":"not enabled"}]}""")
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var outcome = await client.StartWorkspaceAsync("w1");

        Assert.Multiple(() =>
        {
            Assert.That(outcome!.State, Is.EqualTo("Degraded"));
            Assert.That(outcome.FailedApplications.Single().Name, Is.EqualTo("Api"));
            Assert.That(outcome.FailedApplications.Single().Reason, Is.EqualTo("not enabled"));
        });
    }

    [Test]
    public async Task StartWorkspaceAsync_returnsNull_whenTheServerSendsNoContent()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var outcome = await new WorkspaceApiClient(http).StartWorkspaceAsync("w1");

        Assert.That(outcome, Is.Null);
    }

    [Test]
    public async Task StartWorkspaceAsync_returnsNull_whenTheJsonBodyIsEmpty()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("  ")
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var outcome = await new WorkspaceApiClient(http).StartWorkspaceAsync("w1");

        Assert.That(outcome, Is.Null);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly ConcurrentBag<HttpResponseMessage> _responses = new();
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = _response(request);
            _responses.Add(response);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                    response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
