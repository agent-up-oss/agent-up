using AgentUp.AUDebug.Features.Validate.DTOs;
using AgentUp.AUDebug.Features.Validate.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Validate.Providers;

public sealed class MobileValidationRunner : IMobileValidationRunner
{
    public const string ScriptPath = "pwa/flows/run.mjs";

    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugPathValidator _paths;

    public MobileValidationRunner(IAllowlistedProcessRunner processes, IDebugPathValidator paths)
    {
        _processes = processes;
        _paths = paths;
    }

    public Task<ProcessResult> RunAsync(MobileValidationRunDto run, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { ScriptPath, "--out", run.OutDir };
        if (!string.IsNullOrWhiteSpace(run.FlowId))
        {
            arguments.Add("--flow");
            arguments.Add(run.FlowId);
        }

        return _processes.RunAsync(
            new AllowlistedCommand(
                "node",
                arguments,
                _paths.JoinUnderRoot("AgentUp.Mobile.E2E"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["AGENTUP_VALIDATE_REPO"] = _paths.RepositoryRoot
                }),
            cancellationToken);
    }
}
