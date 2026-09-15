using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

// The relay takes a URL from a client and makes the Server fetch it, so the acceptance rules are
// the whole security boundary: anything that is not the loopback address the agent CLI itself
// advertised has to be refused.
[TestFixture]
public sealed class AgentLoginCallbackRelayTests
{
    private const string Expected = "http://localhost:1455/auth/callback";

    [Test]
    public void TryAccept_acceptsTheAdvertisedCallbackAndKeepsTheClisHostSpelling()
    {
        var accepted = AgentLoginCallbackRelay.TryAccept(
            Expected,
            "http://127.0.0.1:1455/auth/callback?code=abc&state=xyz",
            out var target);

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.True);
            Assert.That(target.Host, Is.EqualTo("localhost"), "The CLI bound the prefix it printed, so replay it there");
            Assert.That(target.Port, Is.EqualTo(1455));
            Assert.That(target.AbsolutePath, Is.EqualTo("/auth/callback"));
            Assert.That(target.Query, Does.Contain("code=abc"), "The authorization code has to survive the replay");
        });
    }

    [Test]
    public void TryAccept_refusesAnythingOtherThanTheAdvertisedLoopbackCallback()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentLoginCallbackRelay.TryAccept(Expected, "http://evil.example.com:1455/auth/callback?code=abc", out _),
                Is.False,
                "A remote host must never be fetched on the client's say-so");
            Assert.That(
                AgentLoginCallbackRelay.TryAccept(Expected, "http://localhost:9999/auth/callback?code=abc", out _),
                Is.False,
                "Another port on this host is a different service");
            Assert.That(
                AgentLoginCallbackRelay.TryAccept(Expected, "http://localhost:1455/admin/shutdown", out _),
                Is.False,
                "Another path on the same port is a different endpoint");
            Assert.That(
                AgentLoginCallbackRelay.TryAccept(Expected, "file:///etc/passwd", out _),
                Is.False,
                "Only http and https are fetchable");
            Assert.That(
                AgentLoginCallbackRelay.TryAccept(Expected, "not a url", out _),
                Is.False);
            Assert.That(
                AgentLoginCallbackRelay.TryAccept("not a url", "http://localhost:1455/auth/callback", out _),
                Is.False);
        });
    }

    [Test]
    public async Task RelayAsync_deliversTheRedirectToTheListenerTheCliOpened()
    {
        var (listener, port) = LoopbackListener.Start(bound => $"http://localhost:{bound}/auth/callback/");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            received.TrySetResult(context.Request.Url!.Query);
            context.Response.StatusCode = 200;
            context.Response.Close();
        });

        try
        {
            var relay = new AgentLoginCallbackRelay(new SingleClientFactory(), NullLogger<AgentLoginCallbackRelay>.Instance);

            var relayed = await relay.RelayAsync(
                $"http://localhost:{port}/auth/callback",
                $"http://localhost:{port}/auth/callback?code=abc&state=xyz",
                CancellationToken.None);

            Assert.That(relayed, Is.True);
            Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(10)), Does.Contain("code=abc"));
        }
        finally
        {
            listener.Close();
        }
    }

    [Test]
    public async Task RelayAsync_refusesARedirectThatDoesNotMatchTheAdvertisedCallback()
    {
        var relay = new AgentLoginCallbackRelay(new SingleClientFactory(), NullLogger<AgentLoginCallbackRelay>.Instance);

        var relayed = await relay.RelayAsync(Expected, "http://example.com/auth/callback?code=abc", CancellationToken.None);

        Assert.That(relayed, Is.False);
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
