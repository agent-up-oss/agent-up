using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Features.Capabilities.Models;

namespace AgentUp.CLI.Features.Capabilities.Providers;

public sealed class CapabilitiesArgParser : ICapabilitiesArgParser
{
    public (CapabilitiesCommand? Command, string? Error) Parse(string[] args)
    {
        var verb = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(argument => argument != verb).Skip(1).ToArray();
        return verb switch
        {
            "list" => (new CapabilitiesCommand("list"), null),
            "bootstrap" => (new CapabilitiesCommand("bootstrap"), null),
            "enable" => ParseEnable(remaining),
            "disable" => ParseDisable(remaining),
            _ => (null, CapabilitiesHelp.Text)
        };
    }

    private static (CapabilitiesCommand? Command, string? Error) ParseEnable(string[] args)
    {
        string? id = null;
        string? version = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--version" && index + 1 < args.Length)
            {
                version = args[++index];
                continue;
            }

            if (args[index].StartsWith("--", StringComparison.Ordinal))
                return (null, $"Unknown argument: {args[index]}");

            if (id is not null)
                return (null, "Usage: agent-up capabilities enable <id> [--version <v>]");

            id = args[index];
        }

        return string.IsNullOrWhiteSpace(id)
            ? (null, "Usage: agent-up capabilities enable <id> [--version <v>]")
            : (new CapabilitiesCommand("enable", id, version), null);
    }

    private static (CapabilitiesCommand? Command, string? Error) ParseDisable(string[] args)
    {
        var id = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(id)
            ? (null, "Usage: agent-up capabilities disable <id>")
            : (new CapabilitiesCommand("disable", id), null);
    }
}
