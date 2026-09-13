using AgentUp.Server.Features.ApplicationProxy.Interfaces;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyOriginMapper : IApplicationProxyOriginMapper
{
    public bool ShouldRedirectToOrigin(HttpContext context)
        => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);

    public string OriginRelativeUrl(HttpContext context, string path)
    {
        var relative = string.IsNullOrEmpty(path) ? "/" : path.StartsWith('/') ? path : "/" + path;
        return relative + context.Request.QueryString.Value;
    }

    public void ApplyApplicationPath(HttpContext context, string path)
    {
        var relative = string.IsNullOrEmpty(path) ? "/" : path.StartsWith('/') ? path : "/" + path;
        context.Request.Path = relative;
    }
}
