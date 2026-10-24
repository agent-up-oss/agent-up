using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceLifecycleResultTests
{
    [Test]
    public void Success_defaultsToRunningWithNoFailures()
    {
        var result = WorkspaceLifecycleResult.Success();

        Assert.Multiple(() =>
        {
            Assert.That(result.Found, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State, Is.EqualTo(WorkspaceState.Running));
            Assert.That(result.Failures, Is.Empty);
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public void Success_recordsDegradedFailures()
    {
        var failures = new[] { new WorkspaceApplicationFailure("Api", "not enabled") };

        var result = WorkspaceLifecycleResult.Success(WorkspaceState.Degraded, failures);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State, Is.EqualTo(WorkspaceState.Degraded));
            Assert.That(result.Failures, Is.EqualTo(failures));
        });
    }

    [Test]
    public void Failed_recordsTheErrorAndOptionalFailures()
    {
        var failures = new[] { new WorkspaceApplicationFailure("Api", "not enabled") };

        var withFailures = WorkspaceLifecycleResult.Failed("Workspace could not be started.", failures);
        var withoutFailures = WorkspaceLifecycleResult.Failed("Workspace could not be started.");

        Assert.Multiple(() =>
        {
            Assert.That(withFailures.Succeeded, Is.False);
            Assert.That(withFailures.State, Is.EqualTo(WorkspaceState.Failed));
            Assert.That(withFailures.Failures, Is.EqualTo(failures));
            Assert.That(withoutFailures.Failures, Is.Empty);
            Assert.That(WorkspaceLifecycleResult.NotFound().Found, Is.False);
        });
    }
}
