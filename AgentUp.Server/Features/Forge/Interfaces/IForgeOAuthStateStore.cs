namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeOAuthStateStore
{
    string Issue(string workspaceId);

    string? Consume(string state);
}
