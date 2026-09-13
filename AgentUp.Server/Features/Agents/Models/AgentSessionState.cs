using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Models;

public sealed class AgentSessionState
{
    public AgentSessionState(AgentKind kind, string workingDirectory, IAgentProcessProvider process)
    {
        Kind = kind;
        WorkingDirectory = workingDirectory;
        Process = process;
    }

    public AgentKind Kind { get; }
    public string WorkingDirectory { get; }
    public IAgentProcessProvider Process { get; set; }
    public string? AcpSessionId { get; set; }
    public string State { get; set; } = "starting";
    public string? Error { get; set; }
    public IReadOnlyList<AgentAuthMethodDto> AuthMethods { get; set; } = [];
    public AgentLoginChallengeDto? LoginChallenge { get; set; }
    public SemaphoreSlim PromptGate { get; } = new(1, 1);
    public ConcurrentDictionary<string, PendingAgentPermission> Permissions { get; } = new();
    public CancellationTokenSource Lifetime { get; } = new();
}
