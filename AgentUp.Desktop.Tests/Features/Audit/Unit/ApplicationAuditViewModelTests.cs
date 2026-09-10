using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Providers;
using AgentUp.Desktop.Features.Audit.Services;
using AgentUp.Desktop.Features.Audit.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class ApplicationAuditViewModelTests
{
    [Test]
    public async Task LoadAsync_DisplaysFirstFilteredPageFromLocalCache()
    {
        const string json = """
            {"items":[{"eventId":"e1","timestamp":"2026-08-22T12:00:00Z","kind":"frontend","action":"load_failed","outcome":"failure","details":{"application":"web","message":"Load failed"}}],"nextBefore":null,"nextBeforeEventId":null}
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
            Assert.That(vm.CanGoNext, Is.False);
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NextPage_FetchesAdditionalWindowDataOnDemand()
    {
        var requestCount = 0;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            var isContinuation = request.RequestUri!.Query.Contains("before=", StringComparison.Ordinal);
            var items = isContinuation
                ? Enumerable.Range(ApplicationAuditViewModel.PageSize, ApplicationAuditViewModel.PageSize)
                : Enumerable.Range(0, ApplicationAuditViewModel.PageSize);
            var payload = items.Select(index => new
            {
                eventId = $"e-{index}",
                timestamp = "2026-08-22T12:00:00Z",
                kind = "frontend",
                action = $"event-{index}",
                outcome = "success",
                details = new Dictionary<string, string> { ["application"] = "web", ["message"] = $"event-{index}" }
            });
            var nextBefore = isContinuation ? (string?)null : "2026-08-22T12:00:00Z";
            var nextBeforeEventId = isContinuation ? null : "e-last";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = payload.ToList(),
                    nextBefore,
                    nextBeforeEventId
                }))
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        Assert.That(requestCount, Is.EqualTo(1));
        await vm.NextPageCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(requestCount, Is.EqualTo(3));
            Assert.That(vm.Events, Has.Count.EqualTo(ApplicationAuditViewModel.PageSize));
            Assert.That(vm.Events[0].Message, Is.EqualTo("event-50"));
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
    public async Task KindFilterSelection_ReloadsWindowFromServer()
    {
        var requestCount = 0;
        using var http = new HttpClient(new AsyncStubHandler(_ =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse([
                Event("health-1", "health", "healthy check"),
                Event("frontend-1", "frontend", "load complete")
            ]));
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        foreach (var filter in vm.KindFilters)
            filter.IsSelected = string.Equals(filter.Kind, "health", StringComparison.Ordinal);
        await WaitForLoadingAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(requestCount, Is.EqualTo(2));
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Category, Is.EqualTo("Health"));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StderrFilterSelection_ReloadsWindowFromServer()
    {
        var requestCount = 0;
        using var http = new HttpClient(new AsyncStubHandler(_ =>
        {
            requestCount++;
            return Task.FromResult(JsonResponse([
                Event("stdout-1", "application", "ready", stream: "stdout"),
                Event("stderr-1", "application", "brokers are down", stream: "stderr")
            ]));
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        foreach (var filter in vm.KindFilters)
            filter.IsSelected = string.Equals(filter.Stream, "stderr", StringComparison.Ordinal);
        await WaitForLoadingAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(requestCount, Is.EqualTo(2));
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Category, Is.EqualTo("Stderr"));
            Assert.That(vm.Events[0].Message, Is.EqualTo("brokers are down"));
        });
    }

    [Test]
    public async Task SearchText_FiltersLocalCacheAsInputChanges()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = new[]
                    {
                        EventDto("e1", "frontend", "kafka failure"),
                        EventDto("e2", "health", "Healthy")
                    },
                    nextBefore = (string?)null,
                    nextBeforeEventId = (string?)null
                }))
            })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        vm.SearchText = "kafka";
        await WaitForLoadingAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Message, Is.EqualTo("kafka failure"));
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
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
                return JsonResponse([Event("old-event", "frontend", "old-event")]);
            }
            return JsonResponse([Event("new-event", "frontend", "new-event")]);
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
    public async Task RefreshCommand_ReloadsCurrentWindowWhileStreaming()
    {
        var requestCount = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"nextBefore\":null,\"nextBeforeEventId\":null}")
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsStreaming, Is.True);
            Assert.That(vm.CanRefresh, Is.True);
            Assert.That(requestCount, Is.EqualTo(1));
        });

        await vm.RefreshCommand.Execute().FirstAsync();

        Assert.That(requestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GoToPageOutsideWindow_FetchesOnDemand()
    {
        var requestCount = 0;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            var isContinuation = request.RequestUri!.Query.Contains("before=", StringComparison.Ordinal);
            var start = isContinuation
                ? requestCount * ApplicationAuditViewModel.FetchPageSize
                : 0;
            var count = requestCount == 1
                ? ApplicationAuditViewModel.PageSize
                : ApplicationAuditViewModel.FetchPageSize;
            var items = Enumerable.Range(start, count).Select(index => new
            {
                eventId = $"e-{index}",
                timestamp = "2026-08-22T12:00:00Z",
                kind = "frontend",
                action = $"event-{index}",
                outcome = "success",
                details = new Dictionary<string, string> { ["application"] = "web", ["message"] = $"event-{index}" }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = items.ToList(),
                    nextBefore = "2026-08-22T12:00:00Z",
                    nextBeforeEventId = $"e-{start + count - 1}"
                }))
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        var initialRequests = requestCount;
        for (var page = 1; page < 7; page++)
            await vm.NextPageCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(requestCount, Is.GreaterThan(initialRequests));
            Assert.That(vm.CurrentPage, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task LoadAsync_ReusesLocalCacheWhenReturningToSameApplication()
    {
        var requestCount = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = new[] { EventDto("e1", "frontend", "cached event") },
                    nextBefore = (string?)null,
                    nextBeforeEventId = (string?)null
                }))
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        vm.Deactivate();
        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Message, Is.EqualTo("cached event"));
        });
    }

    [Test]
    public async Task ToggleStreamingCommand_StopsStreamingAndUpdatesButtonText()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"nextBefore\":null,\"nextBeforeEventId\":null}")
            })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http, withStream: true);

        await vm.LoadAsync("ws-1", "web");
        Assert.That(vm.IsStreaming, Is.True);
        Assert.That(vm.StreamingButtonText, Is.EqualTo("Streaming Live"));

        await vm.ToggleStreamingCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsStreaming, Is.False);
            Assert.That(vm.StreamingButtonText, Is.EqualTo("Stopped Streaming"));
        });
    }

    [Test]
    public async Task FirstPageCommand_ReturnsToFirstPage()
    {
        using var http = new HttpClient(new StubHandler(request =>
        {
            var isContinuation = request.RequestUri!.Query.Contains("before=", StringComparison.Ordinal);
            var start = isContinuation ? ApplicationAuditViewModel.PageSize : 0;
            var count = isContinuation ? ApplicationAuditViewModel.FetchPageSize : ApplicationAuditViewModel.PageSize;
            var items = Enumerable.Range(start, count).Select(index => new
            {
                eventId = $"e-{index}",
                timestamp = "2026-08-22T12:00:00Z",
                kind = "frontend",
                action = $"event-{index}",
                outcome = "success",
                details = new Dictionary<string, string> { ["application"] = "web", ["message"] = $"event-{index}" }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = items.ToList(),
                    nextBefore = isContinuation ? null : "2026-08-22T12:00:00Z",
                    nextBeforeEventId = isContinuation ? null : "e-last"
                }))
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        await vm.NextPageCommand.Execute().FirstAsync();
        await vm.FirstPageCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentPage, Is.EqualTo(1));
            Assert.That(vm.Events[0].Message, Is.EqualTo("event-0"));
        });
    }

    [Test]
    public async Task SearchText_WithNoMatches_UsesSearchEmptyMessage()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = new[] { EventDto("e1", "frontend", "kafka failure") },
                    nextBefore = (string?)null,
                    nextBeforeEventId = (string?)null
                }))
            })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        vm.SearchText = "missing-term";
        await WaitForLoadingAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowEmptyState, Is.True);
            Assert.That(vm.EmptyMessage, Does.Contain("missing-term"));
        });
    }

    [Test]
    public async Task PageJumpButtons_IncludeCurrentPageAndNeighbors()
    {
        var requestCount = 0;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            var isContinuation = request.RequestUri!.Query.Contains("before=", StringComparison.Ordinal);
            var start = isContinuation ? requestCount * ApplicationAuditViewModel.FetchPageSize : 0;
            var count = requestCount == 1 ? ApplicationAuditViewModel.PageSize : ApplicationAuditViewModel.FetchPageSize;
            var items = Enumerable.Range(start, count).Select(index => new
            {
                eventId = $"e-{index}",
                timestamp = "2026-08-22T12:00:00Z",
                kind = "frontend",
                action = $"event-{index}",
                outcome = "success",
                details = new Dictionary<string, string> { ["application"] = "web", ["message"] = $"event-{index}" }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = items.ToList(),
                    nextBefore = "2026-08-22T12:00:00Z",
                    nextBeforeEventId = $"e-{start + count - 1}"
                }))
            };
        })) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = CreateViewModel(http);

        await vm.LoadAsync("ws-1", "web");
        for (var page = 1; page < 4; page++)
            await vm.NextPageCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.PageJumpButtons, Is.Not.Empty);
            Assert.That(vm.PageJumpButtons.Single(button => button.IsCurrent).PageNumber, Is.EqualTo(vm.CurrentPage));
            Assert.That(vm.PageJumpButtons.Any(button => button.PageNumber == vm.CurrentPage), Is.True);
        });
    }

    private static async Task WaitForLoadingAsync(ApplicationAuditViewModel vm)
    {
        await Task.Delay(100);
        for (var attempt = 0; attempt < 100 && vm.IsLoading; attempt++)
            await Task.Delay(20);
    }

    private static ApplicationAuditViewModel CreateViewModel(HttpClient http, bool withStream = false)
    {
        var client = new ApplicationAuditApiClient(http);
        return new ApplicationAuditViewModel(
            new ApplicationAuditController(new ApplicationAuditService(client)),
            withStream ? new ApplicationAuditStreamClient(client.Http) : null);
    }

    private static ApplicationAuditEventDto Event(string eventId, string kind, string message, string? stream = null)
    {
        var details = new Dictionary<string, string> { ["application"] = "web", ["message"] = message };
        if (stream is not null)
            details["stream"] = stream;

        return new ApplicationAuditEventDto(
            eventId,
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            kind,
            stream is null ? kind : "application_console_line",
            "success",
            details);
    }

    private static object EventDto(string eventId, string kind, string message, string? stream = null)
    {
        var details = new Dictionary<string, string> { ["application"] = "web", ["message"] = message };
        if (stream is not null)
            details["stream"] = stream;

        return new
        {
            eventId,
            timestamp = "2026-08-22T12:00:00Z",
            kind,
            action = stream is null ? kind : "application_console_line",
            outcome = "success",
            details
        };
    }

    private static HttpResponseMessage JsonResponse(IReadOnlyList<ApplicationAuditEventDto> items)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                items = items.Select(item => new
                {
                    eventId = item.EventId,
                    timestamp = item.Timestamp.ToString("O"),
                    kind = item.Kind,
                    action = item.Action,
                    outcome = item.Outcome,
                    details = item.Details
                }),
                nextBefore = (string?)null,
                nextBeforeEventId = (string?)null
            }))
        };

    private static HttpResponseMessage JsonResponse(object[] items)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                items,
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
