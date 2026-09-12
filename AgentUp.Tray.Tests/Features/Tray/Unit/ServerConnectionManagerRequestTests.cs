using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using AgentUp.Tray.Features.Tray;
using AgentUp.Tray.Tests.Features.Tray.Fake;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class ServerConnectionManagerRequestTests
{
    [Test]
    public async Task StartAsync_pollsTheWorkspacesEndpoint()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.OK);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.StartAsync();
        await exchange.FirstRequest;

        Assert.That(exchange.Requests[0], Is.EqualTo("GET /api/workspaces"));
    }

    [Test]
    public async Task StartAsync_reportsConnectedOnceTheServerAnswers()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.OK);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.StartAsync();

        Assert.That(await Reaches(manager, ServiceState.Connected), Is.EqualTo(ServiceState.Connected));
    }

    [Test]
    public async Task StartAsync_doesNotReportConnectedForAServerErrorStatus()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.InternalServerError);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.StartAsync();
        await exchange.FirstRequest;

        // A reachable-but-unhealthy server is a failed poll, and a failure from Connecting
        // publishes nothing, so the state cannot have moved once the request was answered.
        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Connecting));
    }

    [Test]
    public async Task StartAsync_keepsPollingThroughATransportFailure()
    {
        var exchange = FakeHttpExchange.Failing(new HttpRequestException("connection refused"));
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.StartAsync();
        await exchange.FirstRequest;

        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Connecting));
    }

    [Test]
    public async Task RestartAsync_postsToTheRestartEndpoint()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.Accepted);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.RestartAsync();

        Assert.That(exchange.Requests, Is.EqualTo(new[] { "POST /api/service/restart" }));
    }

    [Test]
    public async Task RestartAsync_reportsRestartingBeforeTheRequest()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.Accepted);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.RestartAsync();

        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Restarting));
    }

    [Test]
    public async Task RestartAsync_survivesTheConnectionDropTheRestartCauses()
    {
        var exchange = FakeHttpExchange.Failing(new HttpRequestException("server went away"));
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.RestartAsync();

        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Restarting));
    }

    [Test]
    public async Task QuitAsync_postsToTheShutdownEndpoint()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.Accepted);
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.QuitAsync();

        Assert.That(exchange.Requests, Is.EqualTo(new[] { "POST /api/service/shutdown" }));
    }

    [Test]
    public async Task QuitAsync_survivesAnUnreachableServer()
    {
        var exchange = FakeHttpExchange.Failing(new HttpRequestException("no listener"));
        using var manager = new ServerConnectionManager(exchange.AsClient());

        await manager.QuitAsync();

        Assert.That(exchange.Requests, Is.EqualTo(new[] { "POST /api/service/shutdown" }));
    }

    [Test]
    public async Task QuitAsync_stopsTheStartedLoops()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.OK);
        using var manager = new ServerConnectionManager(exchange.AsClient());
        await manager.StartAsync();
        await Reaches(manager, ServiceState.Connected);

        await manager.QuitAsync();

        // The poll loop cancels on the shared token, so the shutdown post is the last request.
        Assert.That(exchange.Requests.Last(), Is.EqualTo("POST /api/service/shutdown"));
    }

    [Test]
    public async Task StartAsync_linksTheCallersTokenSoItsCancellationStopsThePolling()
    {
        var exchange = FakeHttpExchange.Answering(HttpStatusCode.OK);
        using var manager = new ServerConnectionManager(exchange.AsClient());
        using var caller = new CancellationTokenSource();

        await manager.StartAsync(caller.Token);
        await exchange.FirstRequest;
        await caller.CancelAsync();

        // The loops observe the linked token, so the poll that was in flight is the last.
        Assert.That(exchange.Requests.Last(), Is.EqualTo("GET /api/workspaces"));
    }

    /// <summary>
    /// Awaits a published state. The manager replays its current state to new subscribers,
    /// so this cannot miss a transition that already happened.
    /// </summary>
    private static Task<ServiceState> Reaches(ServerConnectionManager manager, ServiceState state)
        => manager.State.Where(published => published == state)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(10))
            .ToTask();
}