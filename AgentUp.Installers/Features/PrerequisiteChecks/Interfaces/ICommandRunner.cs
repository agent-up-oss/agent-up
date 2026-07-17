using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;

public interface ICommandRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
