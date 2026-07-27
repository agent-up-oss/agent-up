using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Controllers;

public sealed class CommitsController(
    CommitsEnqueueCommand enqueue,
    CommitsStatusCommand status,
    CommitsChangesCommand changes,
    CommitsInspectCommand inspect,
    CommitsEditCommand edit,
    CommitsEntryCommand entry,
    CommitsGuardCommand guard,
    CommitsNextCommand next,
    CommitsClearCommand clear,
    CommitsOutputService output)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => ResolveCommand(args, enqueue, status, changes, inspect, edit, entry, guard, next, clear, output)(cancellationToken);

    private static Func<CancellationToken, Task<int>> ResolveCommand(
        string[] args,
        CommitsEnqueueCommand enqueue,
        CommitsStatusCommand status,
        CommitsChangesCommand changes,
        CommitsInspectCommand inspect,
        CommitsEditCommand edit,
        CommitsEntryCommand entry,
        CommitsGuardCommand guard,
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
            "changes" => ct => changes.RunAsync(remaining, ct),
            "inspect" => ct => inspect.RunAsync(remaining, ct),
            "edit" => ct => edit.RunAsync(remaining, ct),
            "message" => ct => entry.RunAsync("message", remaining, ct),
            "tests" => ct => entry.RunAsync("tests", remaining, ct),
            "files" => ct => entry.RunAsync("files", remaining, ct),
            "remove" => ct => entry.RunAsync("remove", remaining, ct),
            "restore" => ct => entry.RunAsync("restore", remaining, ct),
            "guard" => ct => guard.RunAsync(remaining, ct),
            "next" => ct => next.RunAsync(remaining, ct),
            "clear" => clear.RunAsync,
            _ => _ => Task.FromResult(output.WriteHelp())
        };
    }
}
