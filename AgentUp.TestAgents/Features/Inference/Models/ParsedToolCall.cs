namespace AgentUp.TestAgents.Features.Inference.Models;

/// <summary>A tool name and argument map extracted from model text.</summary>
public sealed record ParsedToolCall(string Name, IReadOnlyDictionary<string, string> Arguments);
