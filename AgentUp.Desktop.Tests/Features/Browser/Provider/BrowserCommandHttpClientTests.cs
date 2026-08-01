using System.Net;
using AgentUp.Desktop.Features.Browser.Models;
using AgentUp.Desktop.Features.Browser.Providers;

namespace AgentUp.Desktop.Tests.Features.Browser.Provider;

[TestFixture]
public sealed class BrowserCommandHttpClientTests
{
    [Test]
    public async Task GetPendingCommandAsync_ReturnsNull_ForNoContent()
    {
        var client = new BrowserCommandHttpClient(new HttpClient(new StubHandler(
            _ => NoContentResponse()))
        {
            BaseAddress = new Uri("http://localhost")
        });

        var command = await client.GetPendingCommandAsync(["workspace"], 10, CancellationToken.None);

        Assert.That(command, Is.Null);
    }

    [Test]
    public async Task PostCommandResultAsync_PostsCommandResult()
    {
        HttpRequestMessage? request = null;
        var client = new BrowserCommandHttpClient(new HttpClient(new StubHandler(message =>
        {
            request = message;
            return NoContentResponse();
        }))
        {
            BaseAddress = new Uri("http://localhost")
        });

        await client.PostCommandResultAsync(
            new BrowserCommandResultDto(Guid.NewGuid(), true, "{}", null),
            CancellationToken.None);

        Assert.That(request!.RequestUri!.AbsolutePath, Is.EqualTo("/api/browser/command-result"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static HttpResponseMessage NoContentResponse() =>
        new(HttpStatusCode.NoContent);
}
