using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Features.Host.Providers;

public sealed class DebugArgParser : IDebugArgParser
{
    public (DebugCommandDto? Command, string? Error) Parse(string[] args)
    {
        int? timeoutSeconds = null;
        var detach = false;
        var fullPage = false;
        string? password = null;
        string? heading = null;
        var positionals = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg == "--detach")
            {
                detach = true;
                continue;
            }

            if (arg == "--full-page")
            {
                fullPage = true;
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

            if (arg == "--heading")
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    return (null, "Error: --heading requires a value.");
                heading = args[index + 1];
                if (string.IsNullOrWhiteSpace(heading))
                    return (null, "Error: --heading requires a value.");
                index++;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
                return (null, $"Error: unknown argument '{arg}'.");

            positionals.Add(arg);
        }

        if (timeoutSeconds > DebugLayout.MaxTimeoutSeconds)
            return (null, $"Error: --timeout must be between 1 and {DebugLayout.MaxTimeoutSeconds} seconds.");

        return Build(positionals, password, timeoutSeconds, detach, heading, fullPage);
    }

    private static (DebugCommandDto? Command, string? Error) Build(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach,
        string? heading,
        bool fullPage)
    {
        if (positionals.Count == 0 || positionals[0] is "help" or "-h")
            return FinishHelpOrHost("help", timeoutSeconds, password, detach, heading, fullPage);

        var verb = positionals[0];
        if (verb is "up" or "down" or "status")
        {
            if (positionals.Count > 1)
                return (null, $"Error: '{verb}' does not take extra arguments.");
            return FinishHelpOrHost(verb, timeoutSeconds, password, detach, heading, fullPage);
        }

        if (heading is not null || fullPage)
        {
            var docsScreenshot = verb == "docs"
                                 && positionals.Count >= 2
                                 && positionals[1] == "screenshot";
            if (!docsScreenshot)
                return (null, "Error: --heading and --full-page are only valid for docs screenshot.");
        }

        if (verb is "test" or "build")
        {
            if (positionals.Count > 2)
                return (null, $"Error: '{verb}' takes at most one suite name.");
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
            "desktop" => action is "screenshot" or "login" or "start-workspace" or "open-agent",
            "mobile" => action is "screenshot" or "login" or "open-agent",
            _ => action == "screenshot"
        };
        if (!allowed)
            return (null, $"Error: unknown {verb} action '{action}'.");

        string? workspaceName = null;
        string? pagePath = null;
        if (verb == "docs" && action == "screenshot")
        {
            if (positionals.Count > 3)
                return (null, "Error: 'docs screenshot' takes at most one page path.");
            pagePath = positionals.Count == 3 ? positionals[2] : null;
        }
        else if (action == "start-workspace" || (verb == "mobile" && action == "open-agent"))
        {
            if (positionals.Count < 3)
                return (null, $"Error: {verb} {action} requires a workspace name.");
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
                detach,
                PagePath: pagePath,
                Heading: heading,
                FullPage: fullPage),
            null);
    }

    private static (DebugCommandDto? Command, string? Error) FinishHelpOrHost(
        string verb,
        int? timeoutSeconds,
        string? password,
        bool detach,
        string? heading,
        bool fullPage)
    {
        if (heading is not null || fullPage)
            return (null, "Error: --heading and --full-page are only valid for docs screenshot.");

        return Command(verb, timeoutSeconds ?? DebugLayout.DefaultTimeoutSeconds, password, detach);
    }

    private static (DebugCommandDto? Command, string? Error) Command(
        string verb,
        int timeoutSeconds,
        string? password,
        bool detach)
        => (new DebugCommandDto(verb, null, null, null, password, TimeSpan.FromSeconds(timeoutSeconds), detach), null);
}
