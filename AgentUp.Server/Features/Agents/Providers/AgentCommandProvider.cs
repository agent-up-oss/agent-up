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
        if (Path.IsPathRooted(command)) return File.Exists(command);
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(path => File.Exists(Path.Join(path, command)) ||
                         (OperatingSystem.IsWindows() && File.Exists(Path.Join(path, command + ".exe"))));
    }
}
