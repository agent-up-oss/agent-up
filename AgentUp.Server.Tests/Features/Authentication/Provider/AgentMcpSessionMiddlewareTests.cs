using System.Net;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class AgentMcpSessionMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_bindsALiveAgentCredentialToItsWorkspaceAndMember()
    {
        var sessions = new AgentMcpSessionService();
        var token = sessions.Issue("author-1");
        var context = Request("/mcp/browser", token);

        var reached = await InvokeAsync(sessions, context);

        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.True);
            Assert.That(context.User.Identity!.IsAuthenticated, Is.True);
            Assert.That(context.User.FindFirst("workspace")!.Value, Is.EqualTo("author-1"));
            Assert.That(context.User.FindFirst("sub")!.Value, Is.EqualTo("agent-session:author-1"));
            Assert.That(context.User.HasClaim(McpEndpointAuthorizationHandler.PermissionClaimType, "browser.control"), Is.True);
        });
    }

    [Test]
    public async Task InvokeAsync_answersAnUnknownCredentialWith404()
    {
        var context = Request("/mcp/orchestration", "stale");

        var reached = await InvokeAsync(new AgentMcpSessionService(), context);

        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
        });
    }

    [TestCase("/mcp/orchestration", null)]
    [TestCase("/api/workspaces", "stale")]
    public async Task InvokeAsync_leavesOtherCallersAlone(string path, string? token)
    {
        var context = Request(path, token);

        var reached = await InvokeAsync(new AgentMcpSessionService(), context);

        Assert.Multiple(() =>
        {
            Assert.That(reached, Is.True);
            Assert.That(context.User.Identity?.IsAuthenticated ?? false, Is.False);
        });
    }

    [Test]
    public void HeaderName_isTheOneTheServerOffersAgents()
    {
        Assert.That(AgentMcpSessionMiddleware.HeaderName, Is.EqualTo(AgentMcpServerProvider.SessionHeader));
    }

    private static async Task<bool> InvokeAsync(AgentMcpSessionService sessions, HttpContext context)
    {
        var reached = false;
        var members = new TeamMemberService(
            new InMemoryAgentWorkspaceSettingsRepository(),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));
        var middleware = new AgentMcpSessionMiddleware(new AgentMcpSessionController(sessions, members));
        await middleware.InvokeAsync(context, _ => { reached = true; return Task.CompletedTask; });
        return reached;
    }

    private static DefaultHttpContext Request(string path, string? token)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        if (token is not null)
            context.Request.Headers[AgentMcpSessionMiddleware.HeaderName] = token;
        return context;
    }
}
