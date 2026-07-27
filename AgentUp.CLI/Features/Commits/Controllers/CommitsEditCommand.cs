using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsEditCommand(ICommitsUtilityCommandRunner runner)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => runner.RunEditAsync(args, cancellationToken);
}
