using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentWorkspaceSettingsRepository
{
    AgentWorkspaceSettings Get(string workspaceId);

    void Save(AgentWorkspaceSettings settings);

    void Remove(string workspaceId);

    IReadOnlyList<AgentWorkspaceSettings> List();

    bool ServerEnabled { get; }

    void SetServerEnabled(bool enabled);
}
