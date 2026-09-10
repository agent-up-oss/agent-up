using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceApiClientDiagnosticsTests
{
    [Test]
    public async Task GetDiagnosticsAsync_ReturnsDtoForKnownWorkspace()
    {
        var diagnostics = new WorkspaceDiagnosticsDto(
            "w1", "Shop", "Running", "Healthy", DateTimeOffset.UtcNow,
            [new ApplicationDiagnosticsDto("web", "Healthy", "Healthy", ["ready"], false)],
            []);
        using var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(diagnostics))
            };
            return response;
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var result = await client.GetDiagnosticsAsync("w1");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.WorkspaceName, Is.EqualTo("Shop"));
        });
    }

    [Test]
    public async Task GetDiagnosticsAsync_ReturnsNullForMissingWorkspace()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = new WorkspaceApiClient(http);

        var result = await client.GetDiagnosticsAsync("missing");

        Assert.That(result, Is.Null);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly ConcurrentBag<HttpResponseMessage> _responses = new();
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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
