using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentCommandProvider(IConfiguration configuration)
{
    public AgentCommand Get(AgentKind kind)
    {
        var key = kind.ToString();
        var configured = configuration[$"Agents:{key}:Command"];
        var file = configured ?? kind switch
        {
            AgentKind.Codex => "codex-acp",
            AgentKind.Cursor => "agent",
            AgentKind.Claude => "claude-agent-acp",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var arguments = configuration.GetSection($"Agents:{key}:Arguments").Get<string[]>()
            ?? (kind is AgentKind.Cursor ? ["acp"] : []);
        return new AgentCommand(file, arguments);
    }

    public bool IsAvailable(AgentKind kind)
    {
        var command = Get(kind).FileName;
        if (Path.IsPathRooted(command)) return IsExecutable(command);
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(path => extensions.Select(extension => Path.Join(path, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension)))
            .Any(IsExecutable);
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            if (OperatingSystem.IsWindows()) return true;
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
