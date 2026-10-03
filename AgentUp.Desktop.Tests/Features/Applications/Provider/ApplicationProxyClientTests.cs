using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Applications.Providers;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Applications.Provider;

[TestFixture]
public sealed class ApplicationProxyClientTests
{
    [TestCase("http://localhost:5000", false)]
    [TestCase("http://127.0.0.1:5000", false)]
    [TestCase("http://[::1]:5000", false)]
    [TestCase("https://remote.example", true)]
    public void DesktopProxyDecision_FollowsTheActiveConnection(string serverUrl, bool expected)
    {
        Assert.That(MainWindow.ShouldUseApplicationProxy(serverUrl), Is.EqualTo(expected));
    }

    [Test]
    public async Task IssueNavigationUriAsync_UsesTheServerBootstrapAndKeepsTicketInFragment()
    {
        var handler = new TicketHandler(new Queue<string>([
            Ticket("fresh", DateTimeOffset.UtcNow.AddMinutes(1))
        ]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://previous.example/prefix/") };
        var client = new ApplicationProxyClient(http);

        var result = await client.IssueNavigationUriAsync(
            new Uri("https://remote.example/connection/path"), "workspace one", 4312, "/docs?view=full");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Is.EqualTo(1));
            Assert.That(handler.LastHost, Is.EqualTo("remote.example"));
            Assert.That(handler.LastPath, Is.EqualTo("/api/apps/tickets"));
            Assert.That(handler.LastBody, Does.Contain("\"workspaceId\":\"workspace one\""));
            Assert.That(handler.LastBody, Does.Contain("\"allocatedPort\":4312"));
            Assert.That(result.GetLeftPart(UriPartial.Path), Is.EqualTo("https://remote.example/apps/workspace%20one/4312"));
            Assert.That(result.Fragment, Is.EqualTo("#ticket=fresh&return=%2Fdocs%3Fview%3Dfull"));
            Assert.That(result.Query, Is.Empty);
        });
    }

    [Test]
    public void LogicalApplicationUri_MapsTheProxyOriginAndPreservesTheRoute()
    {
        var result = MainWindow.LogicalApplicationUri(
            new Uri("https://remote.example/docs/page?view=full#details"),
            4312);

        Assert.That(result, Is.EqualTo(new Uri("http://127.0.0.1:4312/docs/page?view=full#details")));
    }

    [Test]
    public async Task IssueNavigationUriAsync_ReissuesAnExpiredTicket()
    {
        var now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
        var handler = new TicketHandler(new Queue<string>([
            Ticket("expired", now.AddSeconds(-1)),
            Ticket("replacement", now.AddSeconds(30))
        ]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example/") };
        var client = new ApplicationProxyClient(http, new FixedTimeProvider(now));

        var result = await client.IssueNavigationUriAsync(
            new Uri("https://remote.example/"), "ws", 4312);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Is.EqualTo(2));
            Assert.That(result.Fragment, Is.EqualTo("#ticket=replacement"));
        });
    }

    [Test]
    public void IssueNavigationUriAsync_RejectsAnIncompleteTicket()
    {
        var handler = new TicketHandler(new Queue<string>([
            "{\"ticket\":\"\",\"bootstrapPath\":\"\",\"expiresAt\":\"2026-10-03T12:00:30Z\"}"
        ]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example/") };
        var client = new ApplicationProxyClient(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.IssueNavigationUriAsync(
            new Uri("https://remote.example/"), "ws", 4312));
    }

    [Test]
    public void IssueNavigationUriAsync_RejectsRepeatedExpiredTickets()
    {
        var now = DateTimeOffset.Parse("2026-10-03T12:00:00Z");
        var handler = new TicketHandler(new Queue<string>([
            Ticket("expired-one", now.AddSeconds(-2)),
            Ticket("expired-two", now.AddSeconds(-1))
        ]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example/") };
        var client = new ApplicationProxyClient(http, new FixedTimeProvider(now));

        Assert.ThrowsAsync<InvalidDataException>(() => client.IssueNavigationUriAsync(
            new Uri("https://remote.example/"), "ws", 4312));
        Assert.That(handler.Requests, Is.EqualTo(2));
    }

    private static string Ticket(string value, DateTimeOffset expiresAt) =>
        $$"""{"ticket":"{{value}}","bootstrapPath":"/apps/workspace%20one/4312","expiresAt":"{{expiresAt:O}}"}""";

    private sealed class TicketHandler(Queue<string> responses) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastHost { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            LastHost = request.RequestUri?.Host;
            LastPath = request.RequestUri?.AbsolutePath;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
