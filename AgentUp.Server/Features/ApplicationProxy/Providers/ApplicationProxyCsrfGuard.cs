using AgentUp.Server.Features.ApplicationProxy.Interfaces;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyCsrfGuard : IApplicationProxyCsrfGuard
{
    public bool IsForeignOrigin(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
        {
            return false;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
            return false;

        return !Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
               || !string.Equals(parsed.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }
}
