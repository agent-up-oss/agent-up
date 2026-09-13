namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentClaudeCredentialStore
{
    string? Read();
    void Write(string token);
}
