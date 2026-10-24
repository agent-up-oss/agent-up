using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Models;
using AgentUp.CLI.Features.Capabilities.Services;

namespace AgentUp.CLI.Features.Capabilities.Controllers;

public sealed class CapabilitiesController(
    CapabilitiesListCommand list,
    CapabilitiesEnableCommand enable,
    CapabilitiesDisableCommand disable,
    CapabilitiesBootstrapCommand bootstrap,
    CapabilitiesOutputService output)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => Resolve(args, list, enable, disable, bootstrap, output)(cancellationToken);

    private static Func<CancellationToken, Task<int>> Resolve(
        string[] args,
        CapabilitiesListCommand list,
        CapabilitiesEnableCommand enable,
        CapabilitiesDisableCommand disable,
        CapabilitiesBootstrapCommand bootstrap,
        CapabilitiesOutputService output)
    {
        var subcommand = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(argument => argument != subcommand).Skip(1).ToArray();
        return subcommand switch
        {
            "list" => ct => list.RunAsync(Prepend(subcommand, remaining), ct),
            "enable" => ct => enable.RunAsync(Prepend(subcommand, remaining), ct),
            "disable" => ct => disable.RunAsync(Prepend(subcommand, remaining), ct),
            "bootstrap" => ct => bootstrap.RunAsync(Prepend(subcommand, remaining), ct),
            _ => _ => Task.FromResult(output.Write(CapabilitiesCommandResult.Fail(CapabilitiesHelp.Text, exitCode: 0)))
        };
    }

    private static string[] Prepend(string verb, string[] remaining)
        => new[] { verb }.Concat(remaining).ToArray();
}
