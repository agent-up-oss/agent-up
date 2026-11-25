namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>
/// The team members a task-source token can be saved for. Each workspace is one identity.
/// </summary>
public interface ITaskSourceIdentities
{
    IReadOnlyList<TaskSourcePerson> List();
}

/// <param name="WorkspaceId">The workspace that identity belongs to.</param>
/// <param name="DisplayName">The name the Settings tab and the round banner show.</param>
public sealed record TaskSourcePerson(string WorkspaceId, string DisplayName);
