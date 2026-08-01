using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.Interfaces;
using AgentUp.Desktop.Features.Browser.Services;

namespace AgentUp.Desktop.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserAutomationControllerTests
{
    [Test]
    public void Stop_ForwardsToPollerWithoutThrowing()
    {
        var controller = new BrowserAutomationController(
            new BrowserCommandPoller(
                new AgentUp.Desktop.Features.Browser.Providers.BrowserCommandHttpClient(
                    new HttpClient(new NoContentHandler())
                    {
                        BaseAddress = new Uri("http://localhost")
                    }),
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
        public IReadOnlyCollection<string> ActiveWorkspaceIds => [];

        public Task<string?> EvalAsync(string workspaceId, string script) =>
            Task.FromResult<string?>(null);

        public void NavigateTo(string workspaceId, string? url)
        {
        }
    }
}
