using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.Core.Features.PrerequisiteChecks.Interfaces;

public interface ICommandRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
