using System.Net;
using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.ApplicationProxy.Unit;

[TestFixture]
public sealed class ApplicationProxyServiceTests
{
    [Test]
    public async Task IssueTicket_returnsABootstrapPathForAnOpenHttpPort()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext());

        Assert.That(result.Response, Is.Not.Null);
        Assert.That(result.Response!.BootstrapPath, Is.EqualTo($"/apps/{workspaceId}/{port}"));
        Assert.That(result.Response.Ticket, Has.Length.EqualTo(64));
    }

    [Test]
    public async Task IssueTicket_rejectsAClosedPort()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync(portOpen: false);

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext());

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
    }

    [Test]
    public async Task IssueTicket_rejectsANonHttpPort()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync(protocol: "tcp");

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext());

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task IssueTicket_rejectsAnUnknownWorkspace()
    {
        var (service, _, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(Guid.NewGuid().ToString(), port), ApplicationProxyHarness.LoopbackContext());

        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task IssueTicket_rejectsRemotePlaintext()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var context = RemotePlaintextContext();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), context);

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(result.Title, Is.EqualTo("HTTPS required"));
    }

    [Test]
    public async Task OpenAsync_consumesATicketOnceAndRedirectsToTheOriginRoot()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var context = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);

        await service.OpenAsync(workspaceId, port, string.Empty, context);
        var replay = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, replay);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/"));
        Assert.That(forwarder.Calls, Is.EqualTo(0));
        Assert.That(replay.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task OpenAsync_consumesAPostedTicketWithoutForwardingTheBootstrap()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var context = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        context.Request.Method = HttpMethods.Post;

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/"));
        Assert.That(forwarder.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task OpenAsync_forwardsAuthenticatedNonGetRequestsWithoutRedirecting()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var context = ApplicationProxyHarness.LoopbackContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = $"/apps/{workspaceId}/{port}/echo";
        context.User = AuthenticatedUser();

        await service.OpenAsync(workspaceId, port, "echo", context);

        Assert.That(forwarder.Calls, Is.EqualTo(1));
        Assert.That(forwarder.LastPort, Is.EqualTo(port));
        Assert.That(forwarder.LastPath, Is.EqualTo("/echo"));
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
    }

    [Test]
    public async Task ForwardFallback_usesTheCookieSessionAndRejectsForeignOrigins()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var bootstrap = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, bootstrap);
        var cookie = bootstrap.Response.Headers.SetCookie.ToString();

        var sameOrigin = CreateCookieContext(cookie, HttpMethods.Post);
        await service.ForwardFallbackAsync(sameOrigin);
        var foreign = CreateCookieContext(cookie, HttpMethods.Post);
        foreign.Request.Headers.Origin = "https://evil.example";
        await service.ForwardFallbackAsync(foreign);

        Assert.That(sameOrigin.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
        Assert.That(forwarder.LastPort, Is.EqualTo(port));
        Assert.That(foreign.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ForwardFallback_rejectsReservedServerPaths()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var bootstrap = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, bootstrap);
        var cookie = bootstrap.Response.Headers.SetCookie.ToString();
        var context = CreateCookieContext(cookie, HttpMethods.Get);
        context.Request.Path = "/api/workspaces";

        await service.ForwardFallbackAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(forwarder.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task IssueTicket_rejectsAnOutOfRangePort()
    {
        var (service, workspaceId, _, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, 0), ApplicationProxyHarness.LoopbackContext());

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task OpenAsync_rejectsAnOutOfRangePort()
    {
        var (service, workspaceId, _, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var context = ApplicationProxyHarness.LoopbackContext();
        context.Request.Method = HttpMethods.Get;

        await service.OpenAsync(workspaceId, 70000, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task OpenAsync_servesABootstrapPageWhenGetHasNoTicket()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var context = ApplicationProxyHarness.LoopbackContext();
        context.Request.Method = HttpMethods.Get;

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(context.Response.ContentType, Does.Contain("text/html"));
    }

    [Test]
    public async Task OpenAsync_ignoresQueryStringTickets()
    {
        var (service, workspaceId, port, _, _, clock, tickets) = await ApplicationProxyHarness.CreateAsync();
        var issued = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var context = ApplicationProxyHarness.LoopbackContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = $"/apps/{workspaceId}/{port}";
        context.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["ticket"] = issued });

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Not.Contain("agent-up-proxy="));
        Assert.That(tickets.Consume(issued, clock.GetUtcNow()), Is.Not.Null);
    }

    [Test]
    public async Task OpenAsync_rejectsRemotePlaintextBeforeReadingATicket()
    {
        var (service, workspaceId, port, _, _, clock, tickets) = await ApplicationProxyHarness.CreateAsync();
        var issued = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var context = RemotePlaintextContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers[ApplicationProxyConstants.TicketHeader] = issued;

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(tickets.Consume(issued, clock.GetUtcNow()), Is.Not.Null);
    }

    [Test]
    public async Task OpenAsync_rejectsCrossOriginWrites()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("agent.example");
        context.Request.Headers.Origin = "https://evil.example";
        context.User = AuthenticatedUser();

        await service.OpenAsync(workspaceId, port, "echo", context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task OpenAsync_acceptsAMatchingSessionCookie()
    {
        var (service, workspaceId, port, _, forwarder, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var bootstrap = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, bootstrap);
        var cookie = bootstrap.Response.Headers.SetCookie.ToString();
        var context = CreateCookieContext(cookie, HttpMethods.Get);

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(forwarder.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task OpenAsync_rejectsATicketIssuedForADifferentWorkspace()
    {
        var (service, workspaceId, port, _, _, clock, tickets) = await ApplicationProxyHarness.CreateAsync();
        var ticket = tickets.Issue(new ApplicationProxySession
        {
            WorkspaceId = "other-workspace",
            AllocatedPort = port,
            ExpiresAt = clock.GetUtcNow().AddMinutes(1)
        });
        var context = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);

        await service.OpenAsync(workspaceId, port, string.Empty, context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ForwardFallback_rejectsACookieWhenThePortHasClosed()
    {
        var (service, workspaceId, port, probe, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var bootstrap = CreateTicketContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, bootstrap);
        var cookie = bootstrap.Response.Headers.SetCookie.ToString();
        probe.OpenPorts.Remove(port);
        var context = CreateCookieContext(cookie, HttpMethods.Get);

        await service.ForwardFallbackAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
    }

    private static DefaultHttpContext CreateTicketContext(string path, string ticket)
    {
        var context = ApplicationProxyHarness.LoopbackContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.Headers[ApplicationProxyConstants.TicketHeader] = ticket;
        return context;
    }

    private static DefaultHttpContext CreateCookieContext(string setCookie, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Path = "/";
        context.Request.Host = new HostString("agent.example");
        context.Request.Headers.Cookie = setCookie.Split(';', 2)[0];
        return context;
    }

    private static DefaultHttpContext RemotePlaintextContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("192.168.1.20");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");
        return context;
    }

    private static System.Security.Claims.ClaimsPrincipal AuthenticatedUser()
        => new(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin")],
            "AgentUpBearer"));
}
