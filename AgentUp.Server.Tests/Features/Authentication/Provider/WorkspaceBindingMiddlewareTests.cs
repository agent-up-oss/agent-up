using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class WorkspaceBindingMiddlewareTests
{
    [Test]
    public async Task AllowsRequestWhenPrincipalHasNoWorkspaceBinding()
    {
        var context = new DefaultHttpContext();
        var called = false;
        await new WorkspaceBindingMiddleware().InvokeAsync(context, _ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        Assert.That(called, Is.True);
    }

    [Test]
    public async Task ForbidsWorkspaceRouteThatDoesNotMatchTheBoundWorkspace()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("workspace", "ws-a")]));
        context.Request.Path = "/api/workspaces/ws-b/start";
        context.Request.RouteValues["id"] = "ws-b";

        await new WorkspaceBindingMiddleware().InvokeAsync(context, _ => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }
}
