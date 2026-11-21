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
        var unknown = args.FirstOrDefault(argument => argument.StartsWith("--", StringComparison.Ordinal));
        if (unknown is not null)
            return (null, $"Unknown argument: {unknown}");
        return args.Length == 1 && !string.IsNullOrWhiteSpace(args[0])
            ? (new CapabilitiesCommand("enable", args[0]), null)
            : (null, "Usage: agent-up capabilities enable <id>");
    }

    private static (CapabilitiesCommand? Command, string? Error) ParseDisable(string[] args)
    {
        var id = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(id)
            ? (null, "Usage: agent-up capabilities disable <id>")
            : (new CapabilitiesCommand("disable", id), null);
    }
}
