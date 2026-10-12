using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Turns a model completion into a tool call, or reports that none was parseable.</summary>
public interface IToolCallParser
{
    bool TryParse(string text, out ParsedToolCall? call);
}
