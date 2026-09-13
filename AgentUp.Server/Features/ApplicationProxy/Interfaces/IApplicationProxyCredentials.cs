using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyCredentials
{
    string? ReadTicket(HttpContext context);
    ApplicationProxySession? ReadSession(HttpContext context, DateTimeOffset now);
    void WriteSession(HttpContext context, ApplicationProxySession session);
    void StripTicketFromQuery(HttpContext context);
}
