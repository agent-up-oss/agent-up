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
        var live = false;
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

            if (arg == "--live")
            {
                live = true;
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

        return Build(positionals, password, timeoutSeconds, detach, heading, fullPage, live);
    }

    private static (DebugCommandDto? Command, string? Error) Build(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach,
        string? heading,
        bool fullPage,
        bool live)
    {
        if (positionals.Count == 0 || positionals[0] is "help" or "-h")
            return FinishHelpOrHost("help", timeoutSeconds, password, detach, heading, fullPage, live);

        var verb = positionals[0];
        if (verb is "up" or "down" or "status")
        {
            if (positionals.Count > 1)
                return (null, $"Error: '{verb}' does not take extra arguments.");
            return FinishHelpOrHost(verb, timeoutSeconds, password, detach, heading, fullPage, live);
        }

        if (heading is not null || fullPage)
        {
            var docsScreenshot = verb == "docs"
                                 && positionals.Count >= 2
                                 && positionals[1] == "screenshot";
            if (!docsScreenshot)
                return (null, "Error: --heading and --full-page are only valid for docs screenshot.");
        }

        if (live)
        {
            var screenshotsValidate = verb == "screenshots"
                                      && positionals.Count >= 2
                                      && positionals[1] == "validate";
            if (!screenshotsValidate)
                return (null, "Error: --live is only valid for screenshots validate.");
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

        if (verb == "screenshots")
            return ParseScreenshots(positionals, password, timeoutSeconds, detach, live);

        if (verb == "screens")
            return ParseScreens(positionals, password, timeoutSeconds, detach);

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

    private static (DebugCommandDto? Command, string? Error) ParseScreens(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach)
    {
        var action = positionals.Count >= 2 ? positionals[1] : "all";
        if (action is not ("all" or "desktop" or "mobile" or "compare"))
            return (null, $"Error: unknown screens surface '{action}'.");

        // compare reads the two manifests, so it takes the surface and screen as its own
        // arguments: 'screens compare', 'screens compare mobile', 'screens compare mobile git'.
        if (action == "compare")
            return ParseScreensCompare(positionals, password, timeoutSeconds, detach);

        if (positionals.Count > 3)
            return (null, "Error: 'screens' takes at most a surface and one screen name.");

        var view = positionals.Count == 3 ? positionals[2] : null;
        if (view is not null && action == "all")
            return (null, "Error: 'screens <screen>' needs a surface, such as 'screens desktop git'.");

        return (
            new DebugCommandDto(
                "screens",
                action == "all" ? null : action,
                action,
                null,
                password,
                TimeSpan.FromSeconds(timeoutSeconds ?? DebugLayout.ScreensTimeoutSeconds),
                detach,
                View: view),
            null);
    }

    private static (DebugCommandDto? Command, string? Error) ParseScreensCompare(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach)
    {
        if (positionals.Count > 4)
            return (null, "Error: 'screens compare' takes at most a surface and one screen name.");

        var surface = positionals.Count >= 3 ? positionals[2] : "all";
        if (surface is not ("all" or "desktop" or "mobile"))
            return (null, $"Error: unknown screens surface '{surface}'.");

        var view = positionals.Count == 4 ? positionals[3] : null;
        if (view is not null && surface == "all")
            return (null, "Error: 'screens compare <screen>' needs a surface, such as 'screens compare mobile git'.");

        return (
            new DebugCommandDto(
                "screens",
                surface == "all" ? null : surface,
                "compare",
                null,
                password,
                TimeSpan.FromSeconds(timeoutSeconds ?? DebugLayout.ScreensTimeoutSeconds),
                detach,
                View: view),
            null);
    }

    private static (DebugCommandDto? Command, string? Error) ParseScreenshots(
        IReadOnlyList<string> positionals,
        string? password,
        int? timeoutSeconds,
        bool detach,
        bool live)
    {
        if (positionals.Count < 2)
            return (null, "Error: 'screenshots' requires an action.");

        var action = positionals[1];
        if (action is not ("persist" or "validate" or "desktop" or "mobile"))
            return (null, $"Error: unknown screenshots action '{action}'.");

        string? view = null;
        if (action is "desktop" or "mobile")
        {
            if (positionals.Count > 3)
                return (null, $"Error: 'screenshots {action}' takes at most one view name.");
            view = positionals.Count == 3 ? positionals[2] : null;
        }
        else if (positionals.Count > 2)
        {
            return (null, $"Error: 'screenshots {action}' does not take extra arguments.");
        }

        var timeout = timeoutSeconds
            ?? (action is "persist" or "validate"
                ? DebugLayout.ScreenshotsTimeoutSeconds
                : DebugLayout.DefaultTimeoutSeconds);
        return (
            new DebugCommandDto(
                "screenshots",
                action is "desktop" or "mobile" ? action : "screenshots",
                action,
                null,
                password,
                TimeSpan.FromSeconds(timeout),
                detach,
                View: view,
                Live: live),
            null);
    }

    private static (DebugCommandDto? Command, string? Error) FinishHelpOrHost(
        string verb,
        int? timeoutSeconds,
        string? password,
        bool detach,
        string? heading,
        bool fullPage,
        bool live)
    {
        if (heading is not null || fullPage)
            return (null, "Error: --heading and --full-page are only valid for docs screenshot.");
        if (live)
            return (null, "Error: --live is only valid for screenshots validate.");

        return Command(verb, timeoutSeconds ?? DebugLayout.DefaultTimeoutSeconds, password, detach);
    }

    private static (DebugCommandDto? Command, string? Error) Command(
        string verb,
        int timeoutSeconds,
        string? password,
        bool detach)
        => (new DebugCommandDto(verb, null, null, null, password, TimeSpan.FromSeconds(timeoutSeconds), detach), null);
}
