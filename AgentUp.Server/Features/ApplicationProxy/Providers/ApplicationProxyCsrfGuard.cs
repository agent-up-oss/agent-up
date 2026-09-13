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
               || !OriginsMatch(parsed, context.Request);
    }

    private static bool OriginsMatch(Uri origin, HttpRequest request)
    {
        if (!string.Equals(origin.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(origin.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        var originPort = origin.IsDefaultPort ? DefaultPort(origin.Scheme) : origin.Port;
        var requestPort = request.Host.Port ?? DefaultPort(request.Scheme);
        return originPort == requestPort;
    }

    private static int DefaultPort(string scheme)
        => string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443
            : string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ? 80
            : -1;
}
