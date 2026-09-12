using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Features.Host.Providers;

public sealed class DebugArgParser : IDebugArgParser
{
    public (DebugCommandDto? Command, string? Error) Parse(string[] args)
    {
        int? timeoutSeconds = null;
        var detach = false;
        string? password = null;
        var positionals = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg == "--detach")
            {
                detach = true;
                continue;
            }

            if (arg == "--timeout")
            {
                if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var parsed) || parsed <= 0)
                    return (null, "Error: --timeout requires a positive integer number of seconds.");
                timeoutSeconds = parsed;
                index++;
                continue;
            }

            if (arg == "--password")
            {
                if (index + 1 >= args.Length)
                    return (null, "Error: --password requires a value.");
                password = args[index + 1];
                index++;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
                return (null, $"Error: unknown argument '{arg}'.");

            positionals.Add(arg);
        }

        if (timeoutSeconds > DebugLayout.MaxTimeoutSeconds)
            return (null, $"Error: --timeout must be between 1 and {DebugLayout.MaxTimeoutSeconds} seconds.");

        return Build(positionals, password, timeoutSeconds, detach);
    }

    private static (DebugCommandDto? Command, string? Error) Build(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach)
    {
        if (positionals.Count == 0 || positionals[0] is "help" or "-h")
            return Command("help", timeoutSeconds ?? DebugLayout.DefaultTimeoutSeconds, password, detach);

        var verb = positionals[0];
        if (verb is "up" or "down" or "status")
        {
            if (positionals.Count > 1)
                return (null, $"Error: '{verb}' does not take extra arguments.");
            return Command(verb, timeoutSeconds ?? DebugLayout.DefaultTimeoutSeconds, password, detach);
        }

        if (verb == "test")
        {
            if (positionals.Count > 2)
                return (null, "Error: 'test' takes at most one suite name.");
            var suite = positionals.Count == 2 ? positionals[1] : "all";
            var timeout = timeoutSeconds
                ?? (suite == "all" ? DebugLayout.TestAllTimeoutSeconds : DebugLayout.TestTimeoutSeconds);
            return (new DebugCommandDto(verb, null, null, null, password, TimeSpan.FromSeconds(timeout), detach, suite), null);
        }

        if (verb is not ("desktop" or "mobile" or "docs"))
            return (null, $"Error: unknown command '{verb}'.");

        if (positionals.Count < 2)
            return (null, $"Error: '{verb}' requires an action.");

        var action = positionals[1];
        var allowed = verb switch
        {
            "desktop" => action is "screenshot" or "login" or "start-workspace",
            "mobile" => action is "screenshot" or "login",
            _ => action == "screenshot"
        };
        if (!allowed)
            return (null, $"Error: unknown {verb} action '{action}'.");

        string? workspaceName = null;
        if (action == "start-workspace")
        {
            if (positionals.Count < 3)
                return (null, "Error: desktop start-workspace requires a workspace name.");
            workspaceName = string.Join(' ', positionals.Skip(2));
        }
        else if (positionals.Count > 2)
        {
            return (null, $"Error: '{verb} {action}' does not take extra arguments.");
        }

        return (
            new DebugCommandDto(
                verb,
                verb,
                action,
                workspaceName,
                password,
                TimeSpan.FromSeconds(timeoutSeconds ?? DebugLayout.DefaultTimeoutSeconds),
                detach),
            null);
    }

    private static (DebugCommandDto? Command, string? Error) Command(
        string verb,
        int timeoutSeconds,
        string? password,
        bool detach)
        => (new DebugCommandDto(verb, null, null, null, password, TimeSpan.FromSeconds(timeoutSeconds), detach), null);
}
