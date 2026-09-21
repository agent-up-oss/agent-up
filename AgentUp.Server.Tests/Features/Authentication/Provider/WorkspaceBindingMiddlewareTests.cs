using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Http;

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
        var context = BoundContext("/api/workspaces/ws-b/start");
        context.Request.RouteValues["id"] = "ws-b";

        await new WorkspaceBindingMiddleware().InvokeAsync(context, _ => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AllowsMatchingWorkspaceIdAndIdRoutes()
    {
        var byWorkspaceId = BoundContext("/api/browser/current-url/ws-a");
        byWorkspaceId.Request.RouteValues["workspaceId"] = "ws-a";
        var byId = BoundContext("/api/workspaces/ws-a");
        byId.Request.RouteValues["id"] = "ws-a";

        var called = 0;
        await new WorkspaceBindingMiddleware().InvokeAsync(byWorkspaceId, _ => { called++; return Task.CompletedTask; });
        await new WorkspaceBindingMiddleware().InvokeAsync(byId, _ => { called++; return Task.CompletedTask; });

        Assert.That(called, Is.EqualTo(2));
    }

    [Test]
    public async Task ForbidsMismatchedWorkspaceIdRoute()
    {
        var context = BoundContext("/api/browser/current-url/ws-b");
        context.Request.RouteValues["workspaceId"] = "ws-b";

        await new WorkspaceBindingMiddleware().InvokeAsync(context, _ => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task IgnoresIdRoutesOutsideWorkspacesAndNonStringRouteValues()
    {
        var otherPath = BoundContext("/api/agents/ws-b");
        otherPath.Request.RouteValues["id"] = "ws-b";
        var numericId = BoundContext("/api/workspaces/1");
        numericId.Request.RouteValues["id"] = 1;
        var blank = BoundContext("/api/workspaces/");
        blank.Request.RouteValues["workspaceId"] = "  ";

        var called = 0;
        await new WorkspaceBindingMiddleware().InvokeAsync(otherPath, _ => { called++; return Task.CompletedTask; });
        await new WorkspaceBindingMiddleware().InvokeAsync(numericId, _ => { called++; return Task.CompletedTask; });
        await new WorkspaceBindingMiddleware().InvokeAsync(blank, _ => { called++; return Task.CompletedTask; });
        await new WorkspaceBindingMiddleware().InvokeAsync(BoundContext("/api/workspaces"), _ => { called++; return Task.CompletedTask; });

        Assert.That(called, Is.EqualTo(4));
    }

    private static DefaultHttpContext BoundContext(string path)
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("workspace", "ws-a")]));
        context.Request.Path = path;
        return context;
    }
}
