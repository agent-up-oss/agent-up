using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyBootstrapPage : IApplicationProxyBootstrapPage
{
    public async Task WriteAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'unsafe-inline'; connect-src 'self'";
        await context.Response.WriteAsync(
            $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="referrer" content="no-referrer"><title>Agent-Up</title></head>
            <body>
            <script>
            (function () {
              function sameOriginPath(value) {
                try {
                  var resolved = new URL(value || "/", location.origin);
                  return resolved.origin === location.origin ? resolved.pathname + resolved.search + resolved.hash : "/";
                } catch (_) { return "/"; }
              }
              var prefix = "#ticket=";
              var ticket = location.hash.indexOf(prefix) === 0
                ? decodeURIComponent(location.hash.slice(prefix.length).split("&")[0])
                : "";
              var returnMatch = location.hash.match(/(?:^#|&)return=([^&]*)/);
              var returnPath = sameOriginPath(returnMatch ? decodeURIComponent(returnMatch[1]) : "/");
              history.replaceState(null, "", location.pathname);
              if (!ticket) return;
              var headers = new Headers();
              headers.set("{{ApplicationProxyConstants.TicketHeader}}", ticket);
              fetch(location.pathname, { method: "POST", headers: headers, credentials: "same-origin", redirect: "manual" })
                .then(function (response) {
                  var target = response.headers.get("Location");
                  var fallback = sameOriginPath(target);
                  location.replace(returnPath === "/" ? fallback : returnPath);
                })
                .catch(function () { location.replace("/"); });
            })();
            </script>
            </body>
            </html>
            """);
    }
}
