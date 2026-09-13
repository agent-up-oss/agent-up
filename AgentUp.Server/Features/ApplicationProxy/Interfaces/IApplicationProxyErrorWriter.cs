using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyErrorWriter
{
    Task WriteAsync(HttpContext context, ApplicationProxyAccessResult result);
}
