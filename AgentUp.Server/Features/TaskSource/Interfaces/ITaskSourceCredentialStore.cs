namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>
/// The token the Server holds for the task source.
///
/// It is kept apart from the per-workspace forge credentials on purpose: this one belongs to the
/// Server, and filing it under a workspace id would mean inventing a workspace that does not
/// exist and letting a workspace's sign-out take the Server's backlog down with it.
/// </summary>
public interface ITaskSourceCredentialStore
{
    string? Read(string moduleId);

    void Write(string moduleId, string token);

    void Delete(string moduleId);
}
