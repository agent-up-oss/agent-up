using AgentUp.AUDebug.Features.Validate.DTOs;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Validate.Interfaces;

public interface IMobileValidationRunner
{
    Task<ProcessResult> RunAsync(MobileValidationRunDto run, CancellationToken cancellationToken);
}
