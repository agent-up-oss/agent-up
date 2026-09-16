namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationProxyTransportGuard
{
    bool AllowsCredentials(HttpContext context);
}
