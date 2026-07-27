using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsEnqueueCommand(
    CommitsService service,
    ICommitsArgParser parser,
    CommitsOutputService output)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var (request, error) = parser.Parse(args);
        if (error is not null)
            return output.WriteError(error);

        await service.EnqueueAsync(request!, cancellationToken);
        var status = await service.GetStatusAsync(cancellationToken);
        return output.WriteEnqueueResult(request!.Slice, status.Entries.Count);
    }
}
