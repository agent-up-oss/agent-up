using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;

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
    private readonly Dictionary<string, TaskSourceCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public TaskSourceCredential? Read(string moduleId)
        => _credentials.TryGetValue(moduleId, out var credential) ? credential : null;

    public void Write(string moduleId, TaskSourceCredential credential) => _credentials[moduleId] = credential;

    public void Delete(string moduleId) => _credentials.Remove(moduleId);
}
