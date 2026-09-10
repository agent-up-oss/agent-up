using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Audit.Providers;
using AgentUp.Desktop.Features.Audit.Services;
using AgentUp.Desktop.Features.Audit.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class ApplicationAuditViewModelTests
{
    [Test]
    public async Task LoadAsync_DisplaysOneBoundedPageAndExposesNextPage()
    {
        const string json = """
            {"items":[{"eventId":"e1","timestamp":"2026-08-22T12:00:00Z","kind":"frontend","action":"load_failed","outcome":"failure","details":{"application":"web","message":"Load failed"}}],"nextBefore":"2026-08-22T12:00:00Z","nextBeforeEventId":"e1"}
            """;
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Category, Is.EqualTo("Frontend"));
            Assert.That(vm.Events[0].Message, Is.EqualTo("Load failed"));
            Assert.That(vm.CanGoNext, Is.True);
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NextPage_ReplacesEventsInsteadOfAppending()
    {
        var responses = new Queue<string>([
            """
            {"items":[{"eventId":"e1","timestamp":"2026-08-22T12:00:00Z","kind":"frontend","action":"newest","outcome":"success","details":{"application":"web"}}],"nextBefore":"2026-08-22T12:00:00Z","nextBeforeEventId":"e1"}
            """,
            """
            {"items":[{"eventId":"e0","timestamp":"2026-08-22T11:00:00Z","kind":"frontend","action":"older","outcome":"success","details":{"application":"web"}}],"nextBefore":null,"nextBeforeEventId":null}
            """
        ]);
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responses.Dequeue()) })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        await vm.NextPageCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Category, Is.EqualTo("Frontend"));
            Assert.That(vm.CurrentPage, Is.EqualTo(2));
            Assert.That(vm.CanGoPrevious, Is.True);
            Assert.That(vm.CanGoNext, Is.False);
        });
    }

    [Test]
    public void DefaultKindFilters_ExcludeStdoutAndStderr()
    {
        using var http = new HttpClient();
        var vm = CreateViewModel(http);

        Assert.Multiple(() =>
        {
            Assert.That(vm.KindFilters.Single(option => option.Stream == "stdout").IsSelected, Is.False);
            Assert.That(vm.KindFilters.Single(option => option.Stream == "stderr").IsSelected, Is.False);
            Assert.That(vm.KindFilters.Where(option => option.Stream is null).All(option => option.IsSelected), Is.True);
        });
    }

    [Test]
    public async Task LoadAsync_WithNoSelectedCategories_ShowsEmptyMessageWithoutRequest()
    {
        Uri? requested = null;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            requested = _.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"nextBefore\":null,\"nextBeforeEventId\":null}")
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);
        foreach (var filter in vm.KindFilters)
            filter.IsSelected = false;

        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(requested, Is.Null);
            Assert.That(vm.ShowEmptyState, Is.True);
            Assert.That(vm.EmptyMessage, Is.EqualTo("No diagnostic entries in these categories: []"));
        });
    }

    [Test]
    public async Task KindFilterSelection_ReloadsWithSelectedKinds()
    {
        var requestedQueries = new List<string>();
        var secondLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new AsyncStubHandler(request =>
        {
            requestedQueries.Add(request.RequestUri!.Query);
            if (requestedQueries.Count == 2)
                secondLoad.SetResult();
            return Task.FromResult(JsonResponse("filtered-event", "web"));
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        foreach (var filter in vm.KindFilters)
            filter.IsSelected = string.Equals(filter.Kind, "health", StringComparison.Ordinal);
        await secondLoad.Task;

        Assert.That(requestedQueries[1], Does.Contain("kinds=health"));
    }

    [Test]
    public async Task StderrFilterSelection_ReloadsWithStreamQuery()
    {
        var requestedQueries = new List<string>();
        var stderrLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new AsyncStubHandler(request =>
        {
            requestedQueries.Add(request.RequestUri!.Query);
            if (request.RequestUri.Query.Contains("streams=stderr", StringComparison.Ordinal))
                stderrLoad.TrySetResult();
            return Task.FromResult(JsonResponse("filtered-event", "web"));
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        foreach (var filter in vm.KindFilters)
            filter.IsSelected = string.Equals(filter.Stream, "stderr", StringComparison.Ordinal);
        await stderrLoad.Task;

        var stderrQuery = requestedQueries.Last(query => query.Contains("streams=stderr", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(stderrQuery, Does.Contain("kinds=application"));
            Assert.That(stderrQuery, Does.Contain("streams=stderr"));
        });
    }

    [Test]
    public async Task LoadAsync_DoesNotPublishARequestSupersededByAnotherApplication()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new AsyncStubHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/old", StringComparison.Ordinal))
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
                return JsonResponse("old-event", "old");
            }
            return JsonResponse("new-event", "new");
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        var oldLoad = vm.LoadAsync("ws-1", "old");
        await firstStarted.Task;
        var newLoad = vm.LoadAsync("ws-1", "new");
        releaseFirst.SetResult();
        await Task.WhenAll(oldLoad, newLoad);

        Assert.That(vm.Events.Select(item => item.Message), Is.EqualTo(["new-event"]));
    }

    [Test]
    public async Task ToggleStreaming_DisablesRefreshUntilStopped()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"nextBefore\":null,\"nextBeforeEventId\":null}")
            })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsStreaming, Is.True);
            Assert.That(vm.CanRefresh, Is.False);
            Assert.That(vm.StreamingButtonText, Is.EqualTo("Streaming Live"));
        });

        await vm.ToggleStreamingCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsStreaming, Is.False);
            Assert.That(vm.CanRefresh, Is.True);
            Assert.That(vm.StreamingButtonText, Is.EqualTo("Stopped Streaming"));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
        });
    }

    private static ApplicationAuditViewModel CreateViewModel(HttpClient http, bool withStream = false)
    {
        var client = new ApplicationAuditApiClient(http);
        return new ApplicationAuditViewModel(
            new ApplicationAuditController(new ApplicationAuditService(client)),
            withStream ? new ApplicationAuditStreamClient(client.Http) : null);
    }

    private static HttpResponseMessage JsonResponse(string action, string application)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                items = new[]
                {
                    new
                    {
                        eventId = action,
                        timestamp = "2026-08-22T12:00:00Z",
                        kind = "frontend",
                        action,
                        outcome = "success",
                        details = new Dictionary<string, string> { ["application"] = application, ["message"] = action }
                    }
                },
                nextBefore = (string?)null,
                nextBeforeEventId = (string?)null
            }))
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }

    private sealed class AsyncStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request);
    }
}
