using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Models;

public sealed class AgentSessionState(AgentKind kind, string workingDirectory, IAgentProcessProvider process)
{
    public AgentKind Kind { get; } = kind;
    public IAgentProcessProvider Process { get; } = process;
    public string WorkingDirectory { get; } = workingDirectory;
    public string? AcpSessionId { get; set; }
    public string State { get; set; } = "starting";
    public string? Error { get; set; }
    public IReadOnlyList<AgentAuthMethodDto> AuthMethods { get; set; } = [];
    public SemaphoreSlim PromptGate { get; } = new(1, 1);
    public ConcurrentDictionary<string, PendingAgentPermission> Permissions { get; } = new();
    public CancellationTokenSource Lifetime { get; } = new();
}
