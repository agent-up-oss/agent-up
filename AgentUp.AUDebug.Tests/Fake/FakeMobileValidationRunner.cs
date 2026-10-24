using AgentUp.AUDebug.Features.Validate.DTOs;
using AgentUp.AUDebug.Features.Validate.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeMobileValidationRunner : IMobileValidationRunner
{
    public List<MobileValidationRunDto> Ran { get; } = [];
    public ProcessResult NextResult { get; set; } = new(0, "Mobile validation passed.", "");
    public bool DelayUntilCanceled { get; set; }

    public async Task<ProcessResult> RunAsync(MobileValidationRunDto run, CancellationToken cancellationToken)
    {
        Ran.Add(run);
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return NextResult;
    }
}
