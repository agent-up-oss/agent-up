using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsNextCommand(
    CommitsService service,
    CommitsOutputService output,
    ICommitsFormatParser formatParser)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        => await RunAsync([], cancellationToken);

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var (format, error) = formatParser.Parse(args);
        if (error is not null)
            return output.WriteError(error, format);

        return output.WriteStagingResult(await service.StageNextAsync(cancellationToken), format);
    }
}
