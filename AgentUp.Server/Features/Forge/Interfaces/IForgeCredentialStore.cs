namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeCredentialStore
{
    string? Read(string workspaceId, string moduleId);

    void Write(string workspaceId, string moduleId, string token);

    void Delete(string workspaceId, string moduleId);
}
