using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class WorkspaceBindingMiddleware : IWorkspaceBindingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var boundWorkspace = context.User.FindFirst("workspace")?.Value;
        if (string.IsNullOrWhiteSpace(boundWorkspace))
        {
            await next(context);
            return;
        }

        var routeWorkspace = ReadRouteWorkspace(context);
        if (routeWorkspace is not null
            && !string.Equals(routeWorkspace, boundWorkspace, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private static string? ReadRouteWorkspace(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("workspaceId", out var workspaceId)
            && workspaceId is string workspace
            && !string.IsNullOrWhiteSpace(workspace))
            return workspace;

        if (context.Request.RouteValues.TryGetValue("id", out var id)
            && id is string value
            && !string.IsNullOrWhiteSpace(value)
            && context.Request.Path.StartsWithSegments("/api/workspaces"))
            return value;

        return null;
    }
}
