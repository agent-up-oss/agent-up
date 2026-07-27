using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsChangesCommand(
    CommitsService service,
    CommitsOutputService output,
    ICommitsFormatParser formatParser)
{
    public async Task<int> RunAsync(string[]? args = null, CancellationToken cancellationToken = default)
    {
        var (format, error) = formatParser.Parse(args ?? []);
        if (error is not null)
            return output.WriteError(error, format);

        var result = await service.GetChangesAsync(cancellationToken);
        return output.WriteChanges(result, format);
    }
}
