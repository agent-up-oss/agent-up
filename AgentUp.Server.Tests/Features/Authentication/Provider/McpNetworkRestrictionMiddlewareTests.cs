using System.Net;
using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public class McpNetworkRestrictionMiddlewareTests
{
    private const string RemoteAddress = "192.168.1.20";

    [Test]
    public async Task InvokeAsync_HidesMcpFromNonLoopbackClients()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: false));
        var context = Request("/mcp/browser", RemoteAddress, authenticated: false);

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(called, Is.False);
        });
    }

    [Test]
    public async Task InvokeAsync_HidesMcpFromNonLoopbackClientsEvenWhenTheyAuthenticate()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: false));
        var context = Request("/mcp/browser", RemoteAddress, authenticated: true);

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(called, Is.False);
        });
    }

    [Test]
    public async Task InvokeAsync_AllowsAuthenticatedNonLoopbackClientsWhenRemoteAccessIsEnabled()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: true));
        var context = Request("/mcp/browser", RemoteAddress, authenticated: true);

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.That(called, Is.True);
    }

    [Test]
    public async Task InvokeAsync_HidesMcpFromUnauthenticatedNonLoopbackClientsWhenRemoteAccessIsEnabled()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: true));
        var context = Request("/mcp/browser", RemoteAddress, authenticated: false);

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(called, Is.False);
        });
    }

    [Test]
    public async Task InvokeAsync_LeavesLoopbackMcpAnonymousWhateverRemoteAccessSays()
    {
        var reached = 0;
        var disabled = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: false));
        var enabled = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: true));

        await disabled.InvokeAsync(
            Request("/mcp/browser", "127.0.0.1", authenticated: false),
            _ => { reached++; return Task.CompletedTask; });
        await enabled.InvokeAsync(
            Request("/mcp/browser", "127.0.0.1", authenticated: false),
            _ => { reached++; return Task.CompletedTask; });

        Assert.That(reached, Is.EqualTo(2));
    }

    [Test]
    public async Task InvokeAsync_LeavesNonMcpPathsAloneForNonLoopbackClients()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware(new FakeMcpRemoteAccess(isEnabled: false));
        var context = Request("/api/workspaces", RemoteAddress, authenticated: false);

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.That(called, Is.True);
    }

    private static DefaultHttpContext Request(string path, string? remoteAddress, bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = remoteAddress is null ? null : IPAddress.Parse(remoteAddress);
        if (authenticated)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "user-1")], "TestScheme"));

        return context;
    }
}
