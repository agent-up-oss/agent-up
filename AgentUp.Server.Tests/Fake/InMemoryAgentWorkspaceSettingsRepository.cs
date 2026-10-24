using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryAgentWorkspaceSettingsRepository : IAgentWorkspaceSettingsRepository
{
    private readonly Dictionary<string, AgentWorkspaceSettings> _settings = new(StringComparer.Ordinal);

    public AgentWorkspaceSettings Get(string workspaceId) =>
        _settings.GetValueOrDefault(workspaceId) ?? JsonAgentWorkspaceSettingsRepository.Empty(workspaceId);

    public void Save(AgentWorkspaceSettings settings) => _settings[settings.WorkspaceId] = settings;

    public void Remove(string workspaceId) => _settings.Remove(workspaceId);

    public IReadOnlyList<AgentWorkspaceSettings> List() => [.. _settings.Values];

    public bool ServerEnabled { get; private set; }

    public void SetServerEnabled(bool enabled) => ServerEnabled = enabled;
}
