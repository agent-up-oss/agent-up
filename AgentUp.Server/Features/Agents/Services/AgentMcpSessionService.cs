using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// The credential an ACP agent presents on the Agent-Up MCP servers it is offered. Each live
/// session holds one, and it names only the workspace the session belongs to: that is enough for
/// the MCP endpoints to pin the session to its workspace and narrow it to the workspace's team
/// member grant, the same narrowings a workspace-bound token gets.
/// </summary>
public sealed class AgentMcpSessionService
{
    private readonly ConcurrentDictionary<string, string> _workspaces = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.Ordinal);

    /// <summary>A fresh credential for the workspace's agent. Any earlier one for it stops working.</summary>
    public string Issue(string workspaceId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (_tokens.TryGetValue(workspaceId, out var previous))
            _workspaces.TryRemove(previous, out _);
        _tokens[workspaceId] = token;
        _workspaces[token] = workspaceId;
        return token;
    }

    /// <summary>The workspace a credential belongs to, or null when it was never issued or was revoked.</summary>
    public string? WorkspaceFor(string token) =>
        _workspaces.TryGetValue(token, out var workspaceId) ? workspaceId : null;

    public void Revoke(string workspaceId)
    {
        if (_tokens.TryRemove(workspaceId, out var token))
            _workspaces.TryRemove(token, out _);
    }
}
