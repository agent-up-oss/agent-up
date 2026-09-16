using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentLoginCommandProvider(IConfiguration configuration, AgentSubscriptionAuth subscription)
{
    public AgentLoginCommand Resolve(AgentKind kind, AgentCommand acpCommand, string methodId)
    {
        if (subscription.IsApiKeyMethod(methodId, null))
            throw new InvalidOperationException("Agent-Up signs agents in with a ChatGPT, Cursor, or Claude subscription, not an API key.");

        var configured = ReadConfigured(kind);
        if (configured is not null)
            return WithLoginEnvironment(kind, configured);

        return kind switch
        {
            AgentKind.Cursor => WithLoginEnvironment(kind, new AgentLoginCommand(acpCommand.FileName, LoginArguments(acpCommand.Arguments, "login"), new Dictionary<string, string>())),
            AgentKind.Codex => WithLoginEnvironment(kind, new AgentLoginCommand(ResolveSiblingOrPath(kind, acpCommand.FileName, "codex"), ["login", "--device-auth"], new Dictionary<string, string>())),
            AgentKind.Claude => WithLoginEnvironment(kind, new AgentLoginCommand(ResolveSiblingOrPath(kind, acpCommand.FileName, "claude"), ["setup-token"], new Dictionary<string, string>())),
            _ => throw new InvalidOperationException("The requested agent kind is not supported.")
        };
    }

    private AgentLoginCommand? ReadConfigured(AgentKind kind)
    {
        var configured = configuration[$"Agents:{kind}:LoginCommand"];
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        var arguments = configuration.GetSection($"Agents:{kind}:LoginArguments").Get<string[]>() ?? [];
        return new AgentLoginCommand(configured, arguments, new Dictionary<string, string>());
    }

    /// <summary>
    /// Nothing is watching a browser on the Server host, so ask the CLI not to open one. Both
    /// variables are best-effort: CLIs that ignore them still work, because the sign-in is driven
    /// from the link they print rather than from a browser they launch. Anything a specific
    /// deployment or a specific CLI build needs on top goes in
    /// <c>Agents:{kind}:LoginEnvironment</c>.
    /// </summary>
    private AgentLoginCommand WithLoginEnvironment(AgentKind kind, AgentLoginCommand command)
    {
        var environment = new Dictionary<string, string>(command.Environment, StringComparer.Ordinal)
        {
            ["NO_OPEN_BROWSER"] = "1",
            ["BROWSER"] = "true"
        };
        foreach (var pair in ReadLoginEnvironment(kind))
            environment[pair.Key] = pair.Value;
        return command with { Environment = environment };
    }

    private IReadOnlyDictionary<string, string> ReadLoginEnvironment(AgentKind kind)
    {
        return configuration.GetSection($"Agents:{kind}:LoginEnvironment")
            .GetChildren()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value!, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> LoginArguments(IReadOnlyList<string> acpArguments, string verb)
    {
        var arguments = acpArguments.Where(argument => !argument.Equals("acp", StringComparison.OrdinalIgnoreCase)).ToList();
        arguments.Add(verb);
        return arguments;
    }

    private string ResolveSiblingOrPath(AgentKind kind, string acpFileName, string loginName)
    {
        if (Path.IsPathRooted(acpFileName))
        {
            var directory = Path.GetDirectoryName(acpFileName);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                var sibling = Path.Join(directory, loginName);
                if (IsExecutable(sibling))
                    return sibling;
            }
        }

        if (IsAvailableCommand(loginName))
            return loginName;

        throw new InvalidOperationException(
            $"{loginName} is required for {kind} subscription login. Install it next to the ACP adapter or on PATH, or set Agents:{kind}:LoginCommand.");
    }

    private static bool IsAvailableCommand(string command)
    {
        if (Path.IsPathRooted(command))
            return IsExecutable(command);

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(path => extensions.Select(extension => Path.Join(
                path,
                command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension)))
            .Any(IsExecutable);
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            if (OperatingSystem.IsWindows())
                return true;
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
