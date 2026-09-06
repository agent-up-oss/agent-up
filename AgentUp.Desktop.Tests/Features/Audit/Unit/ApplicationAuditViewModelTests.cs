using System.Net;
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
            {"items":[{"eventId":"e1","timestamp":"2026-08-22T12:00:00Z","action":"load_failed","outcome":"failure","details":{"application":"web","message":"Load failed"}}],"nextBefore":"2026-08-22T12:00:00Z","nextBeforeEventId":"e1"}
            """;
        using var http = new HttpClient(new StubHandler(json)) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = new ApplicationAuditViewModel(new ApplicationAuditController(
            new ApplicationAuditService(new ApplicationAuditApiClient(http))));

        await vm.LoadAsync("ws-1", "web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Events, Has.Count.EqualTo(1));
            Assert.That(vm.Events[0].Action, Is.EqualTo("load_failed"));
            Assert.That(vm.Events[0].Details, Is.EqualTo("message: Load failed"));
            Assert.That(vm.HasMore, Is.True);
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
        var vm = new ApplicationAuditViewModel(new ApplicationAuditController(
            new ApplicationAuditService(new ApplicationAuditApiClient(http))));

        var oldLoad = vm.LoadAsync("ws-1", "old");
        await firstStarted.Task;
        var newLoad = vm.LoadAsync("ws-1", "new");
        releaseFirst.SetResult();
        await Task.WhenAll(oldLoad, newLoad);

        Assert.That(vm.Events.Select(item => item.Action), Is.EqualTo(["new-event"]));
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
                        action,
                        outcome = "success",
                        details = new Dictionary<string, string> { ["application"] = application }
                    }
                },
                nextBefore = (string?)null,
                nextBeforeEventId = (string?)null
            }))
        };

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(CreateResponse(json));

        private static HttpResponseMessage CreateResponse(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private sealed class AsyncStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request);
    }
}
