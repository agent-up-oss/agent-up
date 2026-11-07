using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>An Ollama-compatible chat endpoint with tool calling.</summary>
public interface IInProcessChatModel
{
    bool Configured { get; }

    Task<InProcessChatMessage> ChatAsync(
        IReadOnlyList<InProcessChatMessage> messages,
        IReadOnlyList<InProcessTool> tools,
        CancellationToken cancellationToken);
}
