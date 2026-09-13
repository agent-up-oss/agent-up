using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Test.Interfaces;

public interface IDebugTestProcessRunner
{
    Task<ProcessResult> RunAsync(DebugTestStepDto step, CancellationToken cancellationToken);
}
