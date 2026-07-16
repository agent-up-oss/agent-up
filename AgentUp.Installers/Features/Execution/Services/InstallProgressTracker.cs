using AgentUp.Installers.Features.Execution.Models;

namespace AgentUp.Installers.Features.Execution.Services;

public sealed class InstallProgressTracker
{
    private readonly IReadOnlyList<InstallOperation> _operations;
    private int _completed;

    public InstallProgressTracker(IReadOnlyList<InstallOperation> operations)
    {
        _operations = operations;
    }

    public InstallProgress Complete(InstallOperationKind kind)
    {
        var operation = _operations.First(item => item.Kind == kind);
        _completed++;
        return new InstallProgress(operation.Kind, operation.Title, _completed, _operations.Count);
    }
}
