namespace AgentUp.TestAgents.Features.Inference.Models;

/// <summary>One tool call in the OpenAI shape: the arguments stay a JSON object as text.</summary>
public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>What the model answered: text, tool calls, or both, and why it stopped.</summary>
public sealed record ChatCompletion(string Content, IReadOnlyList<ChatToolCall> ToolCalls, int PromptTokens, int CompletionTokens)
{
    public string FinishReason => ToolCalls.Count > 0 ? "tool_calls" : "stop";
}
