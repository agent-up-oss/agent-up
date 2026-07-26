using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsNextCommand(CommitsService service, CommitsOutputService output)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (await service.HasStagedChangesAsync(cancellationToken))
            return output.WriteError("Staged changes are not yet committed. Commit them first, then run 'agentup commits next'.");
        var result = await service.StageNextAsync(cancellationToken);
        return result is null
            ? output.WriteEmptyQueue("next")
            : output.WriteStagingResult(result);
    }
}
