using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class JsonAgentWorkspaceSettingsRepository : IAgentWorkspaceSettingsRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ConcurrentDictionary<string, AgentWorkspaceSettings> _settings = new(StringComparer.Ordinal);
    private readonly string _path;
    private readonly object _gate = new();

    public JsonAgentWorkspaceSettingsRepository(string dataDirectory)
    {
        _path = Path.Join(dataDirectory, "agent-workspace-settings.json");
        Load();
    }

    public AgentWorkspaceSettings Get(string workspaceId) =>
        _settings.GetValueOrDefault(workspaceId) ?? Empty(workspaceId);

    public void Save(AgentWorkspaceSettings settings)
    {
        _settings[settings.WorkspaceId] = settings;
        Persist();
    }

    public void Remove(string workspaceId)
    {
        _settings.TryRemove(workspaceId, out _);
        Persist();
    }

    public IReadOnlyList<AgentWorkspaceSettings> List() => [.. _settings.Values];

    public static AgentWorkspaceSettings Empty(string workspaceId) => new(
        workspaceId,
        [],
        null,
        false,
        false,
        AssignedIssueOrder.NumberAsc,
        null);

    private void Load()
    {
        if (!File.Exists(_path))
            return;
        var json = File.ReadAllText(_path);
        var items = JsonSerializer.Deserialize<List<AgentWorkspaceSettings>>(json, Json) ?? [];
        foreach (var item in items)
            _settings[item.WorkspaceId] = item;
    }

    private void Persist()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(_settings.Values.ToArray(), Json));
        }
    }
}
