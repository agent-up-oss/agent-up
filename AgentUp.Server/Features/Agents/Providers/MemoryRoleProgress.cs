using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>In-run record of whether a role termination condition has happened.</summary>
public sealed class MemoryRoleProgress : IRoleProgress
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<RoleTerminationKind, byte>> _recorded = new(StringComparer.Ordinal);

    public void Record(string workspaceId, RoleTerminationKind kind) =>
        _recorded.GetOrAdd(workspaceId, _ => new()).TryAdd(kind, 0);

    public bool Satisfied(string workspaceId, RoleTerminationKind kind) =>
        _recorded.TryGetValue(workspaceId, out var set) && set.ContainsKey(kind);
}
