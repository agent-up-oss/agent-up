using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>The configured source, held in memory so a unit test touches no disk.</summary>
internal sealed class MemoryTaskSourceStore : ITaskSourceStore
{
    private TaskSourceSetting? _source;

    public TaskSourceSetting? Read() => _source;

    public void Write(TaskSourceSetting? source) => _source = source;
}

/// <summary>The task source's tokens, held in memory.</summary>
internal sealed class MemoryTaskSourceCredentialStore : ITaskSourceCredentialStore
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public string? Read(string moduleId) => _tokens.TryGetValue(moduleId, out var token) ? token : null;

    public void Write(string moduleId, string token) => _tokens[moduleId] = token;

    public void Delete(string moduleId) => _tokens.Remove(moduleId);
}
