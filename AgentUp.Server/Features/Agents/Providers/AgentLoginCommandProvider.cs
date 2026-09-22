using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentLoginCommandProvider(
    IConfiguration configuration,
    AgentSubscriptionAuth subscription,
    IEnabledCapabilityPackages? packages = null)
{
    public AgentLoginCommand Resolve(string agent, AgentCommand acpCommand, string methodId)
    {
        if (subscription.IsApiKeyMethod(methodId, null))
            throw new InvalidOperationException("Agent-Up signs agents in with a ChatGPT, Cursor, or Claude subscription, not an API key.");

        var configured = ReadConfigured(agent);
        if (configured is not null)
            return WithLoginEnvironment(agent, configured);

        var spec = packages?.GetAgent(agent)?.Login;
        if (spec is not null)
            return WithLoginEnvironment(agent, new AgentLoginCommand(ResolveLoginFile(agent, acpCommand, spec.FileName), spec.Arguments, new Dictionary<string, string>()));

        // First-party module ids only. Listing is by enabled module id; this is not AgentKind.
        return agent.ToLowerInvariant() switch
        {
            "cursor" => WithLoginEnvironment(agent, new AgentLoginCommand(acpCommand.FileName, LoginArguments(acpCommand.Arguments, "login"), new Dictionary<string, string>())),
            "codex" => WithLoginEnvironment(agent, new AgentLoginCommand(ResolveSiblingOrPath(agent, acpCommand.FileName, "codex"), ["login", "--device-auth"], new Dictionary<string, string>())),
            "claude" => WithLoginEnvironment(agent, new AgentLoginCommand(ResolveSiblingOrPath(agent, acpCommand.FileName, "claude"), ["setup-token"], new Dictionary<string, string>())),
            _ => throw new InvalidOperationException("The requested agent is not supported.")
        };
    }

    private AgentLoginCommand? ReadConfigured(string agent)
    {
        var configured = configuration[$"Agents:{agent}:LoginCommand"];
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        var arguments = configuration.GetSection($"Agents:{agent}:LoginArguments").Get<string[]>() ?? [];
        return new AgentLoginCommand(configured, arguments, new Dictionary<string, string>());
    }

    /// <summary>
    /// Nothing is watching a browser on the Server host, so ask the CLI not to open one. Both
    /// variables are best-effort: CLIs that ignore them still work, because the sign-in is driven
    /// from the link they print rather than from a browser they launch. Anything a specific
    /// deployment or a specific CLI build needs on top goes in
    /// <c>Agents:{agent}:LoginEnvironment</c>.
    /// </summary>
    private AgentLoginCommand WithLoginEnvironment(string agent, AgentLoginCommand command)
    {
        var environment = new Dictionary<string, string>(command.Environment, StringComparer.Ordinal)
        {
            ["NO_OPEN_BROWSER"] = "1",
            ["BROWSER"] = "true"
        };
        foreach (var pair in ReadLoginEnvironment(agent))
            environment[pair.Key] = pair.Value;
        return command with { Environment = environment };
    }

    private IReadOnlyDictionary<string, string> ReadLoginEnvironment(string agent)
    {
        return configuration.GetSection($"Agents:{agent}:LoginEnvironment")
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

    private string ResolveLoginFile(string agent, AgentCommand acpCommand, string loginName)
    {
        var acpName = Path.GetFileName(acpCommand.FileName);
        var acpStem = Path.GetFileNameWithoutExtension(acpCommand.FileName);
        if (acpName.Equals(loginName, StringComparison.OrdinalIgnoreCase)
            || acpStem.Equals(loginName, StringComparison.OrdinalIgnoreCase))
            return acpCommand.FileName;
        return ResolveSiblingOrPath(agent, acpCommand.FileName, loginName);
    }

    private string ResolveSiblingOrPath(string agent, string acpFileName, string loginName)
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
            $"{loginName} is required for {agent} subscription login. Install it next to the ACP adapter or on PATH, or set Agents:{agent}:LoginCommand.");
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
