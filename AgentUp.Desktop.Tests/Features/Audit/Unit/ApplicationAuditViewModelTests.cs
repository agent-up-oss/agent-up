using System.Net;
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
            {"items":[{"eventId":"e1","timestamp":"2026-08-22T12:00:00Z","action":"load_failed","outcome":"failure","details":{"application":"web","message":"Load failed"}}],"nextBefore":"2026-08-22T12:00:00Z"}
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

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
