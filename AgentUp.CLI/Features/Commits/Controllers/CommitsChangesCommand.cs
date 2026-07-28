using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsChangesCommand(ICommitsUtilityCommandRunner runner)
{
    public Task<int> RunAsync(string[]? args = null, CancellationToken cancellationToken = default)
        => runner.RunChangesAsync(args ?? [], cancellationToken);
}
