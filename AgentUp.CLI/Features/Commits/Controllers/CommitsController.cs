using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsController(
    CommitsEnqueueCommand enqueue,
    CommitsStatusCommand status,
    CommitsNextCommand next,
    CommitsClearCommand clear,
    CommitsOutputService output)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var subcommand = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(a => a != subcommand).Skip(1).ToArray();

        return subcommand switch
        {
            "enqueue" => await enqueue.RunAsync(remaining, cancellationToken),
            "status" => await status.RunAsync(cancellationToken),
            "next" => await next.RunAsync(cancellationToken),
            "clear" => await clear.RunAsync(cancellationToken),
            _ => output.WriteHelp()
        };
    }
}
