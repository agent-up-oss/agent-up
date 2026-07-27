namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsUtilityCommandRunner
{
    Task<int> RunInspectAsync(string[] args, CancellationToken cancellationToken = default);
    Task<int> RunEditAsync(string[] args, CancellationToken cancellationToken = default);
    Task<int> RunEntryAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
