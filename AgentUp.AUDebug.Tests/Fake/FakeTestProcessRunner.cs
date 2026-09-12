using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Features.Test.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeTestProcessRunner : IDebugTestProcessRunner
{
    public List<DebugTestStepDto> Ran { get; } = [];
    public int NextExitCode { get; set; }
    public string NextOutput { get; set; } = "ok";

    public Task<ProcessResult> RunAsync(DebugTestStepDto step, CancellationToken cancellationToken)
    {
        Ran.Add(step);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProcessResult(NextExitCode, NextOutput, ""));
    }
}
