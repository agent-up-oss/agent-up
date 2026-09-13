using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyOriginMapper : IApplicationProxyOriginMapper
{
    public bool ShouldRedirectToOrigin(HttpContext context)
        => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);

    public bool IsReservedFallbackPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return ApplicationProxyConstants.ReservedFallbackPrefixes.Any(prefix =>
            path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }

    public string OriginRelativeUrl(HttpContext context, string path)
        => LocalPath(path) + context.Request.QueryString.Value;

    public void ApplyApplicationPath(HttpContext context, string path)
        => context.Request.Path = LocalPath(path);

    public Task RedirectToOriginRootAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Location = "/";
        context.Response.StatusCode = StatusCodes.Status302Found;
        return Task.CompletedTask;
    }

    private static string LocalPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains('\0'))
            return "/";

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment is not "." and not "..");
        return "/" + string.Join('/', segments);
    }
}
