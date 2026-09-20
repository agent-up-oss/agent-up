using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Console.Providers;

namespace AgentUp.Desktop.Tests.Features.Console.Provider;

[TestFixture]
public sealed class ConsoleApiClientTests
{
    [Test]
    public async Task GetOutputAsync_requests_the_workspace_application_route_and_deserializes_lines()
    {
        var handler = new RecordingHandler("[\"first\",\"second\"]");
        var client = new ConsoleApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://server.test") });

        var result = await client.GetOutputAsync("workspace-1", "web-app");

        Assert.That(result, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(handler.Path, Is.EqualTo("/api/workspaces/workspace-1/applications/web-app/output"));
    }

    [Test]
    public async Task GetOutputAsync_turns_a_json_null_response_into_an_empty_log()
    {
        var client = new ConsoleApiClient(new HttpClient(new RecordingHandler("null"))
            { BaseAddress = new Uri("https://server.test") });

        Assert.That(await client.GetOutputAsync("one", "api"), Is.Empty);
    }

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}
