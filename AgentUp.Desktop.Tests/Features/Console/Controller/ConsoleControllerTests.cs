using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Console.Controllers;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Console.Services;

namespace AgentUp.Desktop.Tests.Features.Console.Controller;

[TestFixture]
public sealed class ConsoleControllerTests
{
    [Test]
    public async Task GetOutputAsync_returns_lines_from_the_server_boundary()
    {
        var http = new HttpClient(new StaticHandler()) { BaseAddress = new Uri("https://server.test") };
        var controller = new ConsoleController(new ConsoleOutputService(new ConsoleApiClient(http)));

        Assert.That(await controller.GetOutputAsync("one", "api"), Is.EqualTo(new[] { "started" }));
    }

    [Test]
    public void GetOutputAsync_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var http = new HttpClient(new StaticHandler()) { BaseAddress = new Uri("https://server.test") };
        var controller = new ConsoleController(new ConsoleOutputService(new ConsoleApiClient(http)));

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await controller.GetOutputAsync("one", "api", cancellation.Token));
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[\"started\"]", Encoding.UTF8, "application/json")
            });
        }
    }
}
