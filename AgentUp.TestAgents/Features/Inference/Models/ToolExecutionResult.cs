namespace AgentUp.TestAgents.Features.Inference.Models;

/// <summary>Outcome of running one parsed tool against the worktree.</summary>
public sealed record ToolExecutionResult(bool Succeeded, string Output, bool CompletesTask);
