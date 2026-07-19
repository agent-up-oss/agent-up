using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using System.Text;

namespace AgentUp.Installers.Features.Installation.Providers;

public sealed class RequiredCommandRunner : IRequiredCommandRunner
{
    private readonly ICommandRunner _commands;

    public RequiredCommandRunner(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(fileName, arguments, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} {arguments} failed: {CommandOutput(result)}");
    }

    public async Task RunPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var result = await _commands.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}", cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"PowerShell command failed: {CommandOutput(result)}");
    }

    private static string CommandOutput(ProcessResult result)
        => string.Concat(result.Stderr, result.Stdout).Trim();
}
