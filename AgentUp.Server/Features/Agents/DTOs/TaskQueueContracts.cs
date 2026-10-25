namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>One picked task, as the Tasks tab shows it.</summary>
public sealed record QueuedTaskDto(
    string Id,
    string Owner,
    string Repository,
    int Number,
    string Title,
    string Body,
    string Url,
    DateTimeOffset QueuedAt,
    string? TargetWorkspaceId,
    string? HeldByWorkspaceId);

/// <summary>Picks one of the repository's open issues into the queue.</summary>
public sealed record EnqueueTaskRequest(string WorkspaceId, string IssueId, string? TargetWorkspaceId = null);

/// <summary>Moves a task to a position in the queue, counted from zero.</summary>
public sealed record MoveTaskRequest(int Index);

/// <summary>Points a task at one workspace, or at anyone when the id is null.</summary>
public sealed record TargetTaskRequest(string? WorkspaceId);
