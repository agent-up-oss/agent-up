using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentCommandProvider(
    IConfiguration configuration,
    IEnabledCapabilityPackages? packages = null,
    IAgentRuntimePathProvider? runtime = null)
{
    public Task<bool> IsAvailableAsync(string agent, CancellationToken cancellationToken)
        => Task.FromResult(IsLaunchExecutableAvailable(agent));

    public string? LaunchCommandName(string agent)
    {
        var configured = ReadConfigured(agent);
        if (configured is not null)
            return Path.GetFileName(configured.FileName);

        return RawLaunchCommand(agent);
    }

    public Task<AgentCommand?> ResolveAsync(string agent, CancellationToken cancellationToken)
    {
        var configured = ReadConfigured(agent);
        if (configured is not null && Path.IsPathRooted(configured.FileName))
            return Task.FromResult(IsExecutable(configured.FileName) ? configured : null);

        var capability = ResolveCapability(agent);
        if (capability is not null)
            return Task.FromResult<AgentCommand?>(capability);

        if (configured is not null && IsAvailableCommand(configured.FileName))
            return Task.FromResult<AgentCommand?>(configured);

        if (IsScriptedTestAgent(agent) && IsAvailableCommand(AgentUnavailability.ScriptedTestAgent))
            return Task.FromResult<AgentCommand?>(new AgentCommand(AgentUnavailability.ScriptedTestAgent, []));

        return Task.FromResult<AgentCommand?>(null);
    }

    private bool IsLaunchExecutableAvailable(string agent)
    {
        var configured = ReadConfigured(agent);
        if (configured is not null && Path.IsPathRooted(configured.FileName))
            return IsAvailableCommand(configured.FileName);
        if (ResolveCapability(agent) is null && configured is not null)
            return IsAvailableCommand(configured.FileName);

        var raw = RawLaunchCommand(agent);
        return !string.IsNullOrWhiteSpace(raw) && IsAvailableCommand(raw);
    }

    private string? RawLaunchCommand(string agent)
    {
        var launched = packages?.GetAgent(agent)?.Launch();
        if (launched is not null && !string.IsNullOrWhiteSpace(launched.FileName))
            return launched.FileName;

        var command = packages?.GetEnabled(agent)?.Launch?.Command;
        if (!string.IsNullOrWhiteSpace(command))
            return command;

        return IsScriptedTestAgent(agent) ? AgentUnavailability.ScriptedTestAgent : null;
    }

    private static bool IsScriptedTestAgent(string agent) =>
        agent.Equals(AgentUnavailability.ScriptedTestAgent, StringComparison.OrdinalIgnoreCase);

    private AgentCommand? ResolveCapability(string agent)
    {
        var plan = packages?.AgentLaunch(agent);
        if (plan is null || string.IsNullOrWhiteSpace(plan.Command))
            return null;

        return new AgentCommand(plan.Command, plan.Arguments ?? []);
    }

    private AgentCommand? ReadConfigured(string agent)
    {
        var configured = configuration[$"Agents:{agent}:Command"];
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        var arguments = configuration.GetSection($"Agents:{agent}:Arguments").Get<string[]>() ?? [];
        return new AgentCommand(configured, arguments);
    }

    private bool IsAvailableCommand(string command)
    {
        if (Path.IsPathRooted(command))
            return IsExecutable(command);

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        var current = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(runtime?.PathPrefixes() ?? [])
            .Distinct(StringComparer.Ordinal);
        return current
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
