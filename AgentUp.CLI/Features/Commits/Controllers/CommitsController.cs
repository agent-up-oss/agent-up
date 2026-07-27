using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsController(
    CommitsEnqueueCommand enqueue,
    CommitsStatusCommand status,
    CommitsNextCommand next,
    CommitsClearCommand clear,
    CommitsOutputService output)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => ResolveCommand(args, enqueue, status, next, clear, output)(cancellationToken);

    private static Func<CancellationToken, Task<int>> ResolveCommand(
        string[] args,
        CommitsEnqueueCommand enqueue,
        CommitsStatusCommand status,
        CommitsNextCommand next,
        CommitsClearCommand clear,
        CommitsOutputService output)
    {
        var subcommand = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(a => a != subcommand).Skip(1).ToArray();
        return subcommand switch
        {
            "enqueue" => ct => enqueue.RunAsync(remaining, ct),
            "status" => ct => status.RunAsync(remaining, ct),
            "next" => ct => next.RunAsync(remaining, ct),
            "clear" => clear.RunAsync,
            _ => _ => Task.FromResult(output.WriteHelp())
        };
    }
}
