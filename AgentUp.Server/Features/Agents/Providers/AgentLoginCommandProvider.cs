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
            return WithLoginEnvironment(configured);

        return kind switch
        {
            AgentKind.Cursor => WithLoginEnvironment(new AgentLoginCommand(acpCommand.FileName, LoginArguments(acpCommand.Arguments, "login"), new Dictionary<string, string>())),
            AgentKind.Codex => WithLoginEnvironment(new AgentLoginCommand(ResolveSiblingOrPath(kind, acpCommand.FileName, "codex"), ["login", "--device-auth"], new Dictionary<string, string>())),
            AgentKind.Claude => WithLoginEnvironment(new AgentLoginCommand(ResolveSiblingOrPath(kind, acpCommand.FileName, "claude"), ["setup-token"], new Dictionary<string, string>())),
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

    private static AgentLoginCommand WithLoginEnvironment(AgentLoginCommand command)
    {
        var environment = new Dictionary<string, string>(command.Environment, StringComparer.Ordinal)
        {
            ["NO_OPEN_BROWSER"] = "1",
            ["AGENT_CLI_CREDENTIAL_STORE"] = "file"
        };
        return command with { Environment = environment };
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
