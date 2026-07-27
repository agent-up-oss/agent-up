using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsStatusCommand(CommitsService service, CommitsOutputService output)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        => output.WriteStatus(await service.GetStatusAsync(cancellationToken));
}
