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
        using var handler = new DelayedConsoleHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new ConsoleApiClient(http);
        var vm = new ConsoleViewModel(new ConsoleController(new ConsoleOutputService(client)));

        var docsTask = vm.LoadAsync("ws-1", "Docs");
        var mobileTask = vm.LoadAsync("ws-1", "Mobile");
        await Task.WhenAll(docsTask, mobileTask);

        Assert.That(vm.Lines, Is.EqualTo(new[] { "mobile output" }));
    }

    [Test]
    public async Task LoadAsync_marksTruncationWhenServerSendsAMarker()
    {
        using var handler = new MarkerHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var vm = new ConsoleViewModel(new ConsoleController(new ConsoleOutputService(new ConsoleApiClient(http))));

        await vm.LoadAsync("ws-1", "Docs");

        Assert.That(vm.WasTruncated, Is.True);
        Assert.That(vm.Lines[0], Does.StartWith("[truncated]"));
    }

    [Test]
    public async Task LoadAsync_marksTruncationWhenTheClientCapsMaxLines()
    {
        using var handler = new OverflowHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var vm = new ConsoleViewModel(new ConsoleController(new ConsoleOutputService(new ConsoleApiClient(http))));

        await vm.LoadAsync("ws-1", "Docs");

        Assert.That(vm.WasTruncated, Is.True);
        Assert.That(vm.Lines, Has.Count.EqualTo(ConsoleViewModel.MaxLines));
        Assert.That(vm.Lines[0], Does.Not.StartWith("[truncated]"));
    }

    private sealed class MarkerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new { text = "[truncated] 3 earlier lines were dropped.", stream = "stdout" },
                    new { text = "kept", stream = "stdout" }
                })
            });
        }
    }

    private sealed class OverflowHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var lines = Enumerable.Range(0, ConsoleViewModel.MaxLines + 1)
                .Select(index => $"line {index}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(lines)
            });
        }
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
