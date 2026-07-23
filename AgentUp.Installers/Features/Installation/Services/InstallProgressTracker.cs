using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.Installation.Models;

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
