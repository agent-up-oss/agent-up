using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.Services;
using AgentUp.Desktop.Shared.Interfaces;

namespace AgentUp.Desktop.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserAutomationControllerTests
{
    [Test]
    public void Stop_ForwardsToPollerWithoutThrowing()
    {
        using var http = new HttpClient(new NoContentHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var controller = new BrowserAutomationController(
            new BrowserCommandPoller(
                new AgentUp.Desktop.Features.Browser.Providers.BrowserCommandHttpClient(http),
                new EmptyBrowserWindowHost()));

        controller.Stop();

        Assert.Pass();
    }

    private sealed class NoContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(NoContentResponse());

        private static HttpResponseMessage NoContentResponse() =>
            new(System.Net.HttpStatusCode.NoContent);
    }

    private sealed class EmptyBrowserWindowHost : IBrowserWindowHost
    {
        public Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync() =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<string?> EvalAsync(string workspaceId, string script) =>
            Task.FromResult<string?>(null);

        public Task<bool> ActivateWorkspaceUrlAsync(string workspaceId, string url) =>
            Task.FromResult(true);

        public bool NavigateTo(string workspaceId, string? url)
        {
            return true;
        }
    }
}
