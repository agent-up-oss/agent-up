using AgentUp.Desktop.Features.Browser.Interfaces;
using AgentUp.Desktop.Features.Browser.Models;
using AgentUp.Desktop.Features.Browser.Providers;
using AgentUp.Desktop.Features.Browser.Services;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserCommandPollerTests
{
    [Test]
    public async Task AttachPageStateAsync_ReplacesActionDataWithInspectedPageState()
    {
        var state = "{\"title\":\"Saved\",\"interactive\":[]}";
        var command = Command(BrowserCommandKind.Click);
        var poller = CreatePoller(new StateReturningBrowserHost(state));

        var result = await poller.AttachPageStateAsync(
            new BrowserCommandResultDto(command.CommandId, true, "{\"ok\":true}", null),
            command);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(state));
    }

    [Test]
    public async Task AttachPageStateAsync_PreservesFailureWithoutInspecting()
    {
        var command = Command(BrowserCommandKind.Click);
        var host = new StateReturningBrowserHost("{\"title\":\"Saved\"}");
        var poller = CreatePoller(host);

        var result = await poller.AttachPageStateAsync(
            new BrowserCommandResultDto(command.CommandId, false, null, "failed"),
            command);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo("failed"));
        Assert.That(host.EvalCalls, Is.EqualTo(0));
    }

    private static BrowserCommandPoller CreatePoller(IBrowserWindowHost host) =>
        new(
            new BrowserCommandHttpClient(new HttpClient(new NoContentHandler())
            {
                BaseAddress = new Uri("http://localhost")
            }),
            host);

    private static BrowserCommandDto Command(BrowserCommandKind kind) =>
        new(Guid.NewGuid(), "workspace", kind, null, "#save", null, null, 10_000);

    private sealed class NoContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
    }

    private sealed class StateReturningBrowserHost(string state) : IBrowserWindowHost
    {
        public int EvalCalls { get; private set; }

        public IReadOnlyCollection<string> ActiveWorkspaceIds => ["workspace"];

        public Task<string?> EvalAsync(string workspaceId, string script)
        {
            EvalCalls++;
            return Task.FromResult<string?>(state);
        }

        public void NavigateTo(string workspaceId, string? url)
        {
        }
    }
}
