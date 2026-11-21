namespace AgentUp.Desktop.Features.Agents.Models;

/// <summary>
/// One line of an edit's diff: <c>added</c>, <c>deleted</c>, <c>context</c>, or <c>hunk</c> for a
/// section header. Numbers are 1-based and absent where the line does not exist on that side.
/// </summary>
public sealed record AgentDiffLine(string Kind, int? OldNumber, int? NewNumber, string Text);

/// <summary>An edit as the transcript shows it: the file, its lines, and how many changed.</summary>
public sealed record AgentDiff(string? Path, IReadOnlyList<AgentDiffLine> Lines, int Added, int Removed);
