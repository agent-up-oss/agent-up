using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Interfaces;

public interface IServerConnectionStore
{
    ServerSelection Load();
    void Save(ServerSelection selection);
}
