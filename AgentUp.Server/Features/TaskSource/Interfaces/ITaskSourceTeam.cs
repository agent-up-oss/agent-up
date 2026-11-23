namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>
/// The reviewer logins a repository's team uses, so a pull request's requested reviewers can be
/// read as dev review or PM review. <see cref="TaskSourceTeam.Kinds"/> is every reviewer kind
/// the team has on that repository.
/// </summary>
public interface ITaskSourceTeam
{
    Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken);
}

/// <param name="KindByLogin">Host login to <c>devReviewer</c> or <c>pmReviewer</c>.</param>
/// <param name="Kinds">The reviewer kinds the team has on the repository.</param>
public sealed record TaskSourceTeam(IReadOnlyDictionary<string, string> KindByLogin, IReadOnlyCollection<string> Kinds);
