using AgentUp.Server.Features.ApplicationProxy.Interfaces;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyOriginMapper : IApplicationProxyOriginMapper
{
    public bool ShouldRedirectToOrigin(HttpContext context)
        => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);

    public string OriginRelativeUrl(HttpContext context, string path)
        => Normalize(path) + context.Request.QueryString.Value;

    public void ApplyApplicationPath(HttpContext context, string path)
        => context.Request.Path = Normalize(path);

    private static string Normalize(string path)
        => string.IsNullOrEmpty(path) ? "/" : "/" + path.TrimStart('/');
}
