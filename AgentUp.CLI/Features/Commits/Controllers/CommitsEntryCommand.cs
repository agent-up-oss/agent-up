using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsEntryCommand(ICommitsUtilityCommandRunner runner)
{
    public Task<int> RunAsync(string command, string[] args, CancellationToken cancellationToken = default)
        => runner.RunEntryAsync(command, args, cancellationToken);
}
