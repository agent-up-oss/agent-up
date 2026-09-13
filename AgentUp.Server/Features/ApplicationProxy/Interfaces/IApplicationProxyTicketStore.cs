using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyTicketStore
{
    string Issue(ApplicationProxySession session);
    ApplicationProxySession? Consume(string ticket, DateTimeOffset now);
}
