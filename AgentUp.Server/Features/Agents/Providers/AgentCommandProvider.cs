using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentCommandProvider(IConfiguration configuration, IEnumerable<ICapabilityAdapter> adapters)
{
    private readonly IReadOnlyDictionary<string, ICapabilityAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.Descriptor.Id, StringComparer.OrdinalIgnoreCase);

    public async Task<bool> IsAvailableAsync(AgentKind kind, CancellationToken cancellationToken) =>
        await ResolveAsync(kind, cancellationToken) is not null;

    public async Task<AgentCommand?> ResolveAsync(AgentKind kind, CancellationToken cancellationToken)
    {
        var configured = ReadConfigured(kind);
        if (configured is not null && Path.IsPathRooted(configured.FileName))
            return IsExecutable(configured.FileName) ? configured : null;

        var capability = await ResolveCapabilityAsync(kind, cancellationToken);
        if (capability is not null)
            return capability;

        return configured is not null && IsAvailableCommand(configured.FileName) ? configured : null;
    }

    private async Task<AgentCommand?> ResolveCapabilityAsync(AgentKind kind, CancellationToken cancellationToken)
    {
        if (!_adapters.TryGetValue(CapabilityId(kind), out var adapter))
            return null;

        var installed = await adapter.DiscoverAsync(cancellationToken);
        var declaration = new CapabilityDeclaration(
            kind.ToString(),
            adapter.Descriptor.Id,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var validation = await adapter.ValidateAsync(declaration, installed, cancellationToken);
        if (!validation.CanRun)
            return null;

        var plan = await adapter.CreateLaunchPlanAsync(declaration, installed, cancellationToken);
        if (string.IsNullOrWhiteSpace(plan.Command))
            return null;
        return new AgentCommand(plan.Command, plan.Arguments ?? []);
    }

    private AgentCommand? ReadConfigured(AgentKind kind)
    {
        var configured = configuration[$"Agents:{kind}:Command"];
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        var arguments = configuration.GetSection($"Agents:{kind}:Arguments").Get<string[]>() ?? [];
        return new AgentCommand(configured, arguments);
    }

    private static string CapabilityId(AgentKind kind) => kind switch
    {
        AgentKind.Codex => "codex",
        AgentKind.Cursor => "cursor",
        AgentKind.Claude => "claude",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

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
