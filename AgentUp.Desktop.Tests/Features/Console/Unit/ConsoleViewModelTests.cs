using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Console.Controllers;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Console.Services;
using AgentUp.Desktop.Features.Console.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Console.Unit;

[TestFixture]
public sealed class ConsoleViewModelTests
{
    [Test]
    public async Task LoadAsync_KeepsLatestApplicationOutput_WhenLoadsRace()
    {
        var handler = new DelayedConsoleHandler();
        var client = new ConsoleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var vm = new ConsoleViewModel(new ConsoleController(new ConsoleOutputService(client)));

        var docsTask = vm.LoadAsync("ws-1", "Docs");
        var mobileTask = vm.LoadAsync("ws-1", "Mobile");
        await Task.WhenAll(docsTask, mobileTask);

        Assert.That(vm.Lines, Is.EqualTo(new[] { "mobile output" }));
    }

    private sealed class DelayedConsoleHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var delay = path.Contains("/Docs/", StringComparison.Ordinal)
                ? TimeSpan.FromMilliseconds(80)
                : TimeSpan.FromMilliseconds(10);
            var output = path.Contains("/Docs/", StringComparison.Ordinal) ? "docs output" : "mobile output";

            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { output })
            };
        }
    }
}
