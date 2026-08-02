using AgentUp.Desktop.Features.Browser.Models;
using AgentUp.Desktop.Features.Browser.Providers;
using AgentUp.Desktop.Features.Browser.Services;
using AgentUp.Desktop.Shared.Interfaces;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserCommandPollerTests
{
    [Test]
    public async Task AttachPageStateAsync_ReplacesActionDataWithInspectedPageState()
    {
        var state = "{\"title\":\"Saved\",\"interactive\":[]}";
        var command = Command(BrowserCommandKind.Click);
        var host = new StateReturningBrowserHost(state);
        using var http = NoContentHttpClient();
        var poller = new BrowserCommandPoller(
            new BrowserCommandHttpClient(http),
            host);

        var result = await poller.AttachPageStateAsync(
            new BrowserCommandResultDto(command.CommandId, true, "{\"ok\":true}", null),
            command);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(state));
        Assert.That(host.EvalCalls, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task AttachPageStateAsync_ReturnsSettledStateAfterTransientState()
    {
        var landing = "{\"title\":\"Agent-Up\",\"url\":\"http://localhost:3000/developer-guide/mcp\"}";
        var docs = "{\"title\":\"MCP | Agent-Up\",\"url\":\"http://localhost:3000/developer-guide/mcp\"}";
        var command = Command(BrowserCommandKind.Click);
        var host = new SequenceBrowserHost([landing, docs, docs]);
        using var http = NoContentHttpClient();
        var poller = new BrowserCommandPoller(new BrowserCommandHttpClient(http), host);

        var result = await poller.AttachPageStateAsync(
            new BrowserCommandResultDto(command.CommandId, true, "{\"ok\":true}", null),
            command);

        Assert.That(result.Data, Is.EqualTo(docs));
    }

    [Test]
    public async Task AttachPageStateAsync_PreservesFailureWithoutInspecting()
    {
        var command = Command(BrowserCommandKind.Click);
        var host = new StateReturningBrowserHost("{\"title\":\"Saved\"}");
        using var http = NoContentHttpClient();
        var poller = new BrowserCommandPoller(new BrowserCommandHttpClient(http), host);

        var result = await poller.AttachPageStateAsync(
            new BrowserCommandResultDto(command.CommandId, false, null, "failed"),
            command);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo("failed"));
        Assert.That(host.EvalCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_Click_ActivatesWorkspaceUrlAndWaitsBeforeAnimatedClick()
    {
        var delays = new List<int>();
        var target = "{\"success\":true,\"url\":\"http://localhost:5100/settings\"}";
        var state = "{\"title\":\"Settings\",\"url\":\"http://localhost:5100/settings\",\"interactive\":[]}";
        var host = new ClickBrowserHost([target, "{\"ok\":true}", state, state]);
        using var http = NoContentHttpClient();
        var poller = new BrowserCommandPoller(
            new BrowserCommandHttpClient(http),
            host,
            (ms, _) =>
            {
                delays.Add(ms);
                return Task.CompletedTask;
            });

        var result = await poller.ExecuteAsync(Command(BrowserCommandKind.Click), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.EqualTo(state));
            Assert.That(host.ActivatedUrls, Is.EqualTo(["http://localhost:5100/settings"]));
            Assert.That(delays, Does.Contain(200));
            Assert.That(host.EvalScripts[0], Does.Contain("getBoundingClientRect"));
            Assert.That(host.EvalScripts[1], Does.Contain("__agentUpMouse"));
            Assert.That(host.EvalScripts[1], Does.Contain("setTimeout"));
            Assert.That(host.EvalScripts[1], Does.Contain("e.click()"));
        });
    }

    private static HttpClient NoContentHttpClient() =>
        new(new NoContentHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };

    private static BrowserCommandDto Command(BrowserCommandKind kind) =>
        new(Guid.NewGuid(), "workspace", kind, null, "#save", null, null, 10_000);

    private sealed class NoContentHandler : HttpMessageHandler
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "BrowserCommandHttpClient owns and disposes the returned response.")]
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
    }

    private sealed class StateReturningBrowserHost(string state) : IBrowserWindowHost
    {
        public int EvalCalls { get; private set; }

        public Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync() =>
            Task.FromResult<IReadOnlyCollection<string>>(["workspace"]);

        public Task<string?> EvalAsync(string workspaceId, string script)
        {
            EvalCalls++;
            return Task.FromResult<string?>(state);
        }

        public Task<bool> ActivateWorkspaceUrlAsync(string workspaceId, string url) =>
            Task.FromResult(true);

        public bool NavigateTo(string workspaceId, string? url)
        {
            return true;
        }
    }

    private sealed class SequenceBrowserHost(IReadOnlyList<string> states) : IBrowserWindowHost
    {
        private int _index;

        public Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync() =>
            Task.FromResult<IReadOnlyCollection<string>>(["workspace"]);

        public Task<string?> EvalAsync(string workspaceId, string script)
        {
            var value = states[Math.Min(_index, states.Count - 1)];
            _index++;
            return Task.FromResult<string?>(value);
        }

        public Task<bool> ActivateWorkspaceUrlAsync(string workspaceId, string url) =>
            Task.FromResult(true);

        public bool NavigateTo(string workspaceId, string? url)
        {
            return true;
        }
    }

    private sealed class ClickBrowserHost(IReadOnlyList<string> results) : IBrowserWindowHost
    {
        private int _index;

        public List<string> EvalScripts { get; } = [];
        public List<string> ActivatedUrls { get; } = [];

        public Task<IReadOnlyCollection<string>> GetActiveWorkspaceIdsAsync() =>
            Task.FromResult<IReadOnlyCollection<string>>(["workspace"]);

        public Task<string?> EvalAsync(string workspaceId, string script)
        {
            EvalScripts.Add(script);
            var value = results[Math.Min(_index, results.Count - 1)];
            _index++;
            return Task.FromResult<string?>(value);
        }

        public Task<bool> ActivateWorkspaceUrlAsync(string workspaceId, string url)
        {
            ActivatedUrls.Add(url);
            return Task.FromResult(true);
        }

        public bool NavigateTo(string workspaceId, string? url)
        {
            return true;
        }
    }
}
