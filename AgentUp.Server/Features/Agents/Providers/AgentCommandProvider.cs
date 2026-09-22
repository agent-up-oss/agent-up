using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentCommandProvider(
    IConfiguration configuration,
    IEnabledCapabilityPackages? packages = null)
{
    public async Task<bool> IsAvailableAsync(string agent, CancellationToken cancellationToken) =>
        await ResolveAsync(agent, cancellationToken) is not null;

    public Task<AgentCommand?> ResolveAsync(string agent, CancellationToken cancellationToken)
    {
        var configured = ReadConfigured(agent);
        if (configured is not null && Path.IsPathRooted(configured.FileName))
            return Task.FromResult(IsExecutable(configured.FileName) ? configured : null);

        var capability = ResolveCapability(agent);
        if (capability is not null)
            return Task.FromResult<AgentCommand?>(capability);

        return Task.FromResult(configured is not null && IsAvailableCommand(configured.FileName) ? configured : null);
    }

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
