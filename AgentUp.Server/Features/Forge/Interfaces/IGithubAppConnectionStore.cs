using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IGithubAppConnectionStore
{
    GithubAppConnection? Read(string workspaceId);
    GithubAppConnection? FindByState(string state);
    GithubAppConnection? FindBySetupKey(string setupKey);
    void Write(GithubAppConnection connection);
    void Delete(string workspaceId);
}
