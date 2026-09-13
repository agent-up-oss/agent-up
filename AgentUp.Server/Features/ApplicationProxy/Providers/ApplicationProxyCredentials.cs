using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyCredentials(IApplicationProxyCookieProtector protector) : IApplicationProxyCredentials
{
    public string? ReadTicket(HttpContext context)
    {
        var ticket = context.Request.Headers[ApplicationProxyConstants.TicketHeader].ToString();
        return string.IsNullOrWhiteSpace(ticket) ? null : ticket.Trim();
    }

    public ApplicationProxySession? ReadSession(HttpContext context, DateTimeOffset now)
        => protector.Unprotect(context.Request.Cookies[ApplicationProxyConstants.CookieName], now);

    public void WriteSession(HttpContext context, ApplicationProxySession session)
    {
        context.Response.Cookies.Append(
            ApplicationProxyConstants.CookieName,
            protector.Protect(session),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = session.ExpiresAt,
                IsEssential = true
            });
    }

    public void StripTicketFromQuery(HttpContext context)
    {
        var remaining = context.Request.Query
            .Where(pair => !string.Equals(pair.Key, ApplicationProxyConstants.TicketQuery, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value, (pair, value) => new KeyValuePair<string, string?>(pair.Key, value));
        context.Request.QueryString = QueryString.Create(remaining);
    }
}
