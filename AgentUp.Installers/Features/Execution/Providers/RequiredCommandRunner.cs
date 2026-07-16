using AgentUp.Installers.Features.Prerequisites.Services;

namespace AgentUp.Installers.Features.Execution.Providers;

public sealed class RequiredCommandRunner
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
            throw new InvalidOperationException($"{fileName} {arguments} failed: {result.Stderr}{result.Stdout}");
    }

    public async Task RunPowerShellAsync(string command, CancellationToken cancellationToken)
        => await RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", cancellationToken);
}
