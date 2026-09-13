namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyOriginMapper
{
    bool ShouldRedirectToOrigin(HttpContext context);
    bool IsReservedFallbackPath(HttpContext context);
    string OriginRelativeUrl(HttpContext context, string path);
    void ApplyApplicationPath(HttpContext context, string path);
    Task RedirectToOriginRootAsync(HttpContext context);
}
