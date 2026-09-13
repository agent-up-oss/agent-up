using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyCookieProtector
{
    string Protect(ApplicationProxySession session);
    ApplicationProxySession? Unprotect(string? value, DateTimeOffset now);
}
