namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyCsrfGuard
{
    bool IsForeignOrigin(HttpContext context);
}
