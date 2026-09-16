namespace AgentUp.Server.Features.ApplicationProxy.Interfaces;

public interface ILoopbackHttpPortProbe
{
    bool IsListening(int port);
}
