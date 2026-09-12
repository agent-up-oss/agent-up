using System.Diagnostics;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Shared.Interfaces;

public interface IAllowlistedProcessRunner
{
    Process Start(AllowlistedCommand command);
    Task<ProcessResult> RunAsync(AllowlistedCommand command, CancellationToken cancellationToken);
    void KillTree(int pid);
    bool IsRunning(int pid);
}
