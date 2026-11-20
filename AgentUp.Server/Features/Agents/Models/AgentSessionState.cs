using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Models;

public sealed class AgentSessionState
{
    public AgentSessionState(string agent, string workingDirectory, IAgentProcessProvider process)
    {
        Agent = agent;
        WorkingDirectory = workingDirectory;
        Process = process;
    }

    public string Agent { get; }
    public string WorkingDirectory { get; }
    public IAgentProcessProvider Process { get; set; }
    public string? AcpSessionId { get; set; }
    public string State { get; set; } = "starting";
    public string? Error { get; set; }
    public IReadOnlyList<AgentAuthMethodDto> AuthMethods { get; set; } = [];
    public AgentLoginChallengeDto? LoginChallenge { get; set; }

    /// <summary>Whether the agent advertised HTTP MCP at <c>initialize</c>, so Agent-Up's MCP servers can be offered.</summary>
    public bool SupportsHttpMcp { get; set; }

    /// <summary>The credential this session presents on the Agent-Up MCP servers it is offered.</summary>
    public string? McpSessionToken { get; set; }

    /// <summary>
    /// Open while a sign-in is running, so a code or an intercepted redirect posted over HTTP
    /// reaches the CLI that is already waiting for it.
    /// </summary>
    public AgentLoginInbox? LoginInbox { get; set; }
    public SemaphoreSlim PromptGate { get; } = new(1, 1);
    public ConcurrentDictionary<string, PendingAgentPermission> Permissions { get; } = new();
    public CancellationTokenSource Lifetime { get; } = new();
    public IAgentPermissionPolicy? PermissionPolicy { get; set; }
    public List<string> PermissionRuleIds { get; } = [];
    public string? LoopBlock { get; set; }
    public JsonElement? LastPromptResult { get; set; }
    public TaskCompletionSource<JsonElement>? PromptCompletion { get; set; }
    public int TranscriptEvents { get; set; }
    public ConcurrentQueue<string> Steering { get; } = new();
    public int ConsecutiveNoProgress { get; set; }
    public bool SawToolCall { get; set; }

    /// <summary>The latest update the agent sent for each tool call in the running turn.</summary>
    public ConcurrentDictionary<string, JsonElement> ToolCalls { get; } = new(StringComparer.Ordinal);

    /// <summary>The last tool failure, and how many times in a row the agent has repeated it.</summary>
    public string? LastFailure { get; set; }

    public int RepeatedFailures { get; set; }

    /// <summary>What the agent said in the turn that ran last, so a review can carry its verdict.</summary>
    public System.Text.StringBuilder LastReply { get; } = new();
}
