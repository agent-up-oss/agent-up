using AgentUp.Server.Features.TaskSource.Interfaces;

namespace AgentUp.Server.Tests.Support;

/// <summary>No team, for a host that is not asking which reviews a repository owes.</summary>
internal sealed class EmptyTaskSourceTeam : ITaskSourceTeam
{
    public Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken)
        => Task.FromResult(new TaskSourceTeam(new Dictionary<string, string>(), []));
}

/// <summary>The task source's tokens, held in memory.</summary>
internal sealed class MemoryTaskSourceCredentialStore : ITaskSourceCredentialStore
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public string? Read(string moduleId) => _tokens.TryGetValue(moduleId, out var token) ? token : null;

    public void Write(string moduleId, string token) => _tokens[moduleId] = token;

    public void Delete(string moduleId) => _tokens.Remove(moduleId);
}
