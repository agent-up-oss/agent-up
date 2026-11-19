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
        => Task.FromResult(InProcessAgents.IsInProcess(agent)
            ? InProcessConfigured(agent)
            : IsLaunchExecutableAvailable(agent));

    /// <summary>An in-process agent has no executable; it is available once its model is named.</summary>
    public bool InProcessConfigured(string agent) =>
        InProcessAgents.IsInProcess(agent)
        && !string.IsNullOrWhiteSpace(configuration[InProcessAgents.OllamaModelSetting]);

    public string? LaunchCommandName(string agent)
    {
        var configured = ReadConfigured(agent);
        if (configured is not null)
            return Path.GetFileName(configured.FileName);

        return RawLaunchCommand(agent);
    }

    public Task<AgentCommand?> ResolveAsync(string agent, CancellationToken cancellationToken)
    {
        var configured = WithCapabilityLaunch(agent, ReadConfigured(agent));
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
        // An enabled built-in capability that still lacks software or a setting cannot start, and
        // an executable its own Nix packages deliver need not be on the Server's PATH.
        var readiness = packages?.Readiness(agent);
        if (readiness is { Enabled: true, CanRun: false } && configured is null)
            return false;
        if (configured is null && DeliveredByNix(agent))
            return true;
        if (ResolveCapability(agent) is null && configured is not null)
            return IsAvailableCommand(configured.FileName);

        var raw = RawLaunchCommand(agent);
        return !string.IsNullOrWhiteSpace(raw) && IsAvailableCommand(raw);
    }

    private string? RawLaunchCommand(string agent)
    {
        var launched = packages?.GetAgent(agent)?.Launch(new Dictionary<string, string>());
        if (launched is not null && !string.IsNullOrWhiteSpace(launched.FileName))
            return launched.FileName;

        return IsScriptedTestAgent(agent) ? AgentUnavailability.ScriptedTestAgent : null;
    }

    private bool DeliveredByNix(string agent)
    {
        var module = packages?.GetAgent(agent);
        var fileName = RawLaunchCommand(agent);
        return module is not null
               && module.Software.Any(item => item.DeliveredByNix
                                              && item.Executable.Equals(fileName, StringComparison.Ordinal));
    }

    private static bool IsScriptedTestAgent(string agent) =>
        agent.Equals(AgentUnavailability.ScriptedTestAgent, StringComparison.OrdinalIgnoreCase);

    private AgentCommand? ResolveCapability(string agent)
    {
        var plan = packages?.AgentLaunch(agent);
        if (plan is null || string.IsNullOrWhiteSpace(plan.Command))
            return null;

        return new AgentCommand(plan.Command, plan.Arguments ?? [], plan.Environment);
    }

    /// <summary>
    /// <c>Agents:{id}:Command</c> replaces only the executable. An enabled capability still supplies
    /// its arguments when the override names none, and always its launch environment - OpenCode's
    /// endpoint configuration travels there, and an agent started without it has no model.
    /// </summary>
    private AgentCommand? WithCapabilityLaunch(string agent, AgentCommand? configured)
    {
        if (configured is null)
            return null;
        var module = packages?.GetAgent(agent);
        if (module is null)
            return configured;
        var plan = packages!.AgentLaunch(agent);
        var arguments = configured.Arguments.Count > 0
            ? configured.Arguments
            : module.Launch(new Dictionary<string, string>()).Arguments;
        return configured with { Arguments = arguments, Environment = plan?.Environment };
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
