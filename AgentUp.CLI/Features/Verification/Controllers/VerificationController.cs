using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

/// <summary>
/// Routes <c>agentup verify</c> subcommands.
/// </summary>
public sealed class VerificationController(
    VerifyPlanCommand plan,
    VerifyRunCommand run,
    VerifyGuardCommand guard,
    VerifyOutputService output,
    string worktreePath)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => Resolve(args, plan, run, guard, output, worktreePath)(cancellationToken);

    private static Func<CancellationToken, Task<int>> Resolve(
        string[] args,
        VerifyPlanCommand plan,
        VerifyRunCommand run,
        VerifyGuardCommand guard,
        VerifyOutputService output,
        string worktreePath)
    {
        var subcommand = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(arg => arg != subcommand).Skip(1).ToArray();
        var format = HasHookFormat(args) ? VerifyOutputFormat.Hook : VerifyOutputFormat.Text;
        var runMissing = args.Contains("--run", StringComparer.Ordinal);

        return subcommand switch
        {
            "plan" => ct => plan.RunAsync(worktreePath, ct),
            "run" => ct => run.RunAsync(worktreePath, FirstPositional(remaining), ct),
            "guard" => ct => guard.RunAsync(worktreePath, format, runMissing, ct),
            _ => _ => Task.FromResult(WriteHelp(output))
        };
    }

    private static bool HasHookFormat(string[] args)
    {
        if (args.Contains("--format=hook", StringComparer.Ordinal))
            return true;

        var index = Array.IndexOf(args, "--format");
        return index >= 0 && index + 1 < args.Length && args[index + 1] == "hook";
    }

    private static string? FirstPositional(string[] args)
        => args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));

    private static int WriteHelp(VerifyOutputService output)
        => output.WriteError(
            """
            Usage: agentup verify <command>

              plan                     Show the checks the current changes require
              run [<check-id>]         Run the required checks (or one of them) and record receipts
              guard [--run]            Report whether every required check is proven
                                       --run also executes what is missing
                    [--format hook]    Terse output for a Stop hook: silent when satisfied

            Check selection comes from the 'verification' section of agent-up.json.
            """);
}
