using System.Diagnostics;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeProcessRunner : IAllowlistedProcessRunner
{
    public List<AllowlistedCommand> Started { get; } = [];
    public List<AllowlistedCommand> Ran { get; } = [];
    public List<int> Killed { get; } = [];
    public ProcessResult NextResult { get; set; } = new(0, "1\n", "");
    public Action<AllowlistedCommand>? OnRun { get; set; }
    public bool Running { get; set; } = true;
    public Func<AllowlistedCommand, Process>? StartOverride { get; set; }

    public Process Start(AllowlistedCommand command)
    {
        Started.Add(command);
        if (StartOverride is not null)
            return StartOverride(command);

        return Process.Start(new ProcessStartInfo
        {
            FileName = "true",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
    }

    public Task<ProcessResult> RunAsync(AllowlistedCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Ran.Add(command);
        OnRun?.Invoke(command);
        return Task.FromResult(NextResult);
    }

    public void KillTree(int pid) => Killed.Add(pid);

    public bool IsRunning(int pid) => Running;
}
