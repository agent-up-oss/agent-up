using AgentUp.Server.Features.ApplicationProxy.Models;
using Yarp.ReverseProxy.Forwarder;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyRequestTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
        proxyRequest.Headers.Host = null;
        proxyRequest.Headers.Remove("Authorization");
        StripProxyCookie(proxyRequest);
    }

    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken)
    {
        var continueResponse = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
        if (proxyResponse is null)
            return continueResponse;

        httpContext.Response.Headers.Remove("X-Frame-Options");
        StripFrameAncestors(httpContext.Response);
        RewriteLocation(httpContext, proxyResponse);
        StripCookieDomains(httpContext.Response);
        return continueResponse;
    }

    private static void StripProxyCookie(HttpRequestMessage proxyRequest)
    {
        if (!proxyRequest.Headers.TryGetValues("Cookie", out var cookies))
            return;

        var remaining = cookies
            .SelectMany(header => header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(part => !part.StartsWith(ApplicationProxyConstants.CookieName + "=", StringComparison.Ordinal))
            .ToArray();
        proxyRequest.Headers.Remove("Cookie");
        if (remaining.Length > 0)
            proxyRequest.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", remaining));
    }

    private static void StripFrameAncestors(HttpResponse response)
    {
        if (!response.Headers.TryGetValue("Content-Security-Policy", out var values))
            return;

        var rewritten = values
            .Select(value => string.Join(';', (value ?? string.Empty).Split(';', StringSplitOptions.TrimEntries)
                .Where(directive => !directive.StartsWith("frame-ancestors", StringComparison.OrdinalIgnoreCase))))
            .Where(value => value.Length > 0)
            .ToArray();
        response.Headers.Remove("Content-Security-Policy");
        if (rewritten.Length > 0)
            response.Headers.ContentSecurityPolicy = rewritten;
    }

    private static void RewriteLocation(HttpContext httpContext, HttpResponseMessage proxyResponse)
    {
        if (proxyResponse.Headers.Location is null)
            return;
        if (!httpContext.Items.TryGetValue(ApplicationProxyConstants.DestinationPortItem, out var portValue)
            || portValue is not int port)
            return;

        var location = proxyResponse.Headers.Location;
        if (!location.IsAbsoluteUri)
            return;
        if (!IsLoopbackDestination(location, port))
            return;

        var rewritten = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{location.PathAndQuery}";
        httpContext.Response.Headers.Location = rewritten;
    }

    private static bool IsLoopbackDestination(Uri location, int port)
        => location.Port == port
           && (location.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || location.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || location.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase));

    private static void StripCookieDomains(HttpResponse response)
    {
        if (!response.Headers.TryGetValue("Set-Cookie", out var cookies))
            return;

        var rewritten = cookies
            .Select(value => string.Join("; ", (value ?? string.Empty).Split(';', StringSplitOptions.TrimEntries)
                .Where(part => !part.StartsWith("domain=", StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        response.Headers.Remove("Set-Cookie");
        foreach (var cookie in rewritten)
            response.Headers.Append("Set-Cookie", cookie);
    }
}
