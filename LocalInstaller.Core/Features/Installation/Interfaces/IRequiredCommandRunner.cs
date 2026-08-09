namespace AgentUp.Installers.Features.Installation.Interfaces;

public interface IRequiredCommandRunner
{
    Task RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);

    Task RunPowerShellAsync(string script, CancellationToken cancellationToken = default);
}
