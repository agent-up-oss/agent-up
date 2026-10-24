using AgentUp.AUDebug.Features.Validation.DTOs;
using AgentUp.AUDebug.Features.Validation.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeDesktopValidationLaunch : IDesktopValidationLaunch
{
    public List<DesktopValidationLaunchSpec> Ran { get; } = [];
    public ProcessResult NextResult { get; set; } = new(0, "Desktop validation passed.", "");
    public string ResolvedOutput { get; set; } = "/tmp/desktop-validation";
    public bool DelayUntilCanceled { get; set; }

    public string ResolveOutputPath(string? outputPath)
        => string.IsNullOrWhiteSpace(outputPath) ? ResolvedOutput : outputPath;

    public async Task<ProcessResult> RunAsync(DesktopValidationLaunchSpec spec, CancellationToken cancellationToken)
    {
        Ran.Add(spec);
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return NextResult;
    }
}
