using System.Net;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public class McpNetworkRestrictionMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_HidesMcpFromNonLoopbackClients()
    {
        var called = false;
        var middleware = new McpNetworkRestrictionMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp/browser";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");

        await middleware.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(called, Is.False);
        });
    }
}
