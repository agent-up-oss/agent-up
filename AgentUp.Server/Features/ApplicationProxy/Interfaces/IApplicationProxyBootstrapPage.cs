namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyBootstrapPage
{
    Task WriteAsync(HttpContext context);
}
