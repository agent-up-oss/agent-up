using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Executes allowlisted tools inside a worktree after the ACP client has granted permission.</summary>
public interface IWorktreeTools
{
    IReadOnlySet<string> Names { get; }

    ToolExecutionResult Execute(string worktree, ParsedToolCall call);
}
