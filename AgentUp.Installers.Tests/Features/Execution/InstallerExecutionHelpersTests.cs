using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Prerequisites;

namespace AgentUp.Installers.Tests.Features.Execution;

[TestFixture]
public class InstallerExecutionHelpersTests
{
    [Test]
    public void InstallProgressTracker_reportsCompletedOperations()
    {
        var operations = new[]
        {
            new InstallOperation(InstallOperationKind.ValidatePrerequisites, "Validate", false),
            new InstallOperation(InstallOperationKind.InstallFiles, "Install", true)
        };
        var tracker = new InstallProgressTracker(operations);

        var first = tracker.Complete(InstallOperationKind.ValidatePrerequisites);
        var second = tracker.Complete(InstallOperationKind.InstallFiles);

        Assert.That(first.CompletedOperations, Is.EqualTo(1));
        Assert.That(first.TotalOperations, Is.EqualTo(2));
        Assert.That(second.CompletedOperations, Is.EqualTo(2));
    }

    [Test]
    public void RequiredCommandRunner_throwsWhenCommandFails()
    {
        var commands = new RecordingCommandRunner(new ProcessResult(2, "stdout", "stderr"));
        var runner = new RequiredCommandRunner(commands);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runner.RunAsync("systemctl", "start agent-up-server.service", CancellationToken.None));

        Assert.That(ex!.Message, Is.EqualTo("systemctl start agent-up-server.service failed: stderrstdout"));
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        private readonly ProcessResult _result;

        public RecordingCommandRunner(ProcessResult result)
        {
            _result = result;
        }

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}
