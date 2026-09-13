namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface IApplicationHttpForwarder
{
    Task ForwardAsync(HttpContext context, int allocatedPort);
}
