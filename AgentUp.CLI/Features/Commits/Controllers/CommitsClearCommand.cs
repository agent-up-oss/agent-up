using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsClearCommand(CommitsService service, CommitsOutputService output)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await service.ClearAsync(cancellationToken);
        return output.WriteCleared();
    }
}
