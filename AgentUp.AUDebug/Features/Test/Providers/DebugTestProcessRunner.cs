using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Features.Test.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Test.Providers;

public sealed class DebugTestProcessRunner : IDebugTestProcessRunner
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugPathValidator _paths;

    public DebugTestProcessRunner(IAllowlistedProcessRunner processes, IDebugPathValidator paths)
    {
        _processes = processes;
        _paths = paths;
    }

    public Task<ProcessResult> RunAsync(DebugTestStepDto step, CancellationToken cancellationToken)
    {
        var workingDirectory = step.WorkingDirectory is "." or ""
            ? _paths.RepositoryRoot
            : _paths.JoinUnderRoot(step.WorkingDirectory);
        return _processes.RunAsync(
            new AllowlistedCommand(step.FileName, step.Arguments, workingDirectory),
            cancellationToken);
    }
}
