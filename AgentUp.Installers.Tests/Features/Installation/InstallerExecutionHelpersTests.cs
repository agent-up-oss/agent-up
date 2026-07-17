using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.PrerequisiteChecks;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.Installation;

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
