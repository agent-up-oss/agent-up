using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.ApplicationProxy.Unit;

[TestFixture]
public sealed class ApplicationProxyServiceTests
{
    [Test]
    public async Task IssueTicket_returnsABootstrapPathForAnOpenHttpPort()
    {
        var (service, workspaceId, port, _, _, _) = await ApplicationProxyHarness.CreateAsync();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port));

        Assert.That(result.Response, Is.Not.Null);
        Assert.That(result.Response!.BootstrapPath, Is.EqualTo($"/apps/{workspaceId}/{port}"));
        Assert.That(result.Response.Ticket, Has.Length.EqualTo(64));
    }

    [Test]
    public async Task IssueTicket_rejectsAClosedPort()
    {
        var (service, workspaceId, port, _, _, _) = await ApplicationProxyHarness.CreateAsync(portOpen: false);

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port));

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
    }

    [Test]
    public async Task IssueTicket_rejectsANonHttpPort()
    {
        var (service, workspaceId, port, _, _, _) = await ApplicationProxyHarness.CreateAsync(protocol: "tcp");

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port));

        Assert.That(result.Response, Is.Null);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task IssueTicket_rejectsAnUnknownWorkspace()
    {
        var (service, _, port, _, _, _) = await ApplicationProxyHarness.CreateAsync();

        var result = service.IssueTicket(new ApplicationProxyTicketRequest(Guid.NewGuid().ToString(), port));

        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task OpenAsync_consumesATicketOnceAndRedirectsToTheOriginRoot()
    {
        var (service, workspaceId, port, _, forwarder, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port)).Response!.Ticket;
        var context = CreateGetContext($"/apps/{workspaceId}/{port}", ticket);

        await service.OpenAsync(workspaceId, port, string.Empty, context);
        var replay = CreateGetContext($"/apps/{workspaceId}/{port}", ticket);
        await service.OpenAsync(workspaceId, port, string.Empty, replay);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/"));
        Assert.That(forwarder.Calls, Is.EqualTo(0));
        Assert.That(replay.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task OpenAsync_forwardsAuthenticatedNonGetRequestsWithoutRedirecting()
    {
        var (service, workspaceId, port, _, forwarder, _) = await ApplicationProxyHarness.CreateAsync();
        var context = new DefaultHttpContext();
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
        var (service, workspaceId, port, _, forwarder, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port)).Response!.Ticket;
        var bootstrap = CreateGetContext($"/apps/{workspaceId}/{port}", ticket);
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

    private static DefaultHttpContext CreateGetContext(string path, string ticket)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["ticket"] = ticket });
        return context;
    }

    private static DefaultHttpContext CreateCookieContext(string setCookie, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/";
        context.Request.Host = new HostString("agent.example");
        context.Request.Headers.Cookie = setCookie.Split(';', 2)[0];
        return context;
    }

    private static System.Security.Claims.ClaimsPrincipal AuthenticatedUser()
        => new(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin")],
            "AgentUpBearer"));
}
