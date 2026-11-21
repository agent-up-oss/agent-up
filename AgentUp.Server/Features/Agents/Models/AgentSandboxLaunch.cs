namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The process the Server actually starts for a sandboxed agent. <see cref="Environment"/> is
/// the complete environment: the Server's own is not inherited.
/// </summary>
public sealed record AgentSandboxLaunch(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    string WorkingDirectory);

/// <summary>
/// The host has no way to confine an agent, so the agent is not started. Running it with the
/// Server's own permissions instead would let a reviewer write the clone it may only read.
/// </summary>
public sealed class AgentSandboxUnavailableException(string message) : InvalidOperationException(message);
