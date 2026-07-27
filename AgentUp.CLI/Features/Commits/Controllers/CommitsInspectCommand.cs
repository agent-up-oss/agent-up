using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsInspectCommand(ICommitsUtilityCommandRunner runner)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => runner.RunInspectAsync(args, cancellationToken);
}
