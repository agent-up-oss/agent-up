using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>
/// The tokens the Server holds for the task source, keyed by team member (workspace id).
///
/// A legacy file keyed by host (<c>github</c>, <c>forgejo</c>) is still readable: those entries
/// keep listing repositories until a member saves their own token, and they never name a member
/// on the repository's access banners.
/// </summary>
public interface ITaskSourceCredentialStore
{
    IReadOnlyList<string> Keys();

    TaskSourceCredential? Read(string key);

    void Write(string key, TaskSourceCredential credential);

    void Delete(string key);
}
