using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceStartEvaluationTests
{
    [Test]
    public void StateFrom_isRunning_whenEveryApplicationIsRunning()
    {
        Assert.That(
            WorkspaceStartEvaluation.StateFrom([Running("Web"), Running("Api")]),
            Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public void StateFrom_isDegraded_whenSomeApplicationsFailed()
    {
        Assert.That(
            WorkspaceStartEvaluation.StateFrom([Running("Web"), Failed("Api")]),
            Is.EqualTo(WorkspaceState.Degraded));
    }

    [Test]
    public void StateFrom_isFailed_whenNoApplicationIsRunning()
    {
        Assert.That(
            WorkspaceStartEvaluation.StateFrom([Failed("Web"), Failed("Api")]),
            Is.EqualTo(WorkspaceState.Failed));
    }

    [Test]
    public void StateFrom_isRunning_whenTheWorkspaceHasNoApplications()
    {
        Assert.That(WorkspaceStartEvaluation.StateFrom([]), Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public void Failures_useCapabilityMessagesDesktopHostAndDefaultReasons()
    {
        var workspace = new Workspace
        {
            Id = "ws",
            DisplayName = "Shop",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications =
            [
                Failed("Api", capability: new CapabilityStatusDto("dotnet", "10.0.x", false, ["not enabled"])),
                Failed("Editor", kind: ApplicationKind.Desktop),
                Failed("Web")
            ]
        };

        var failures = WorkspaceStartEvaluation.Failures(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(failures.Select(failure => failure.Reason), Is.EqualTo(new[]
            {
                "not enabled",
                "Desktop applications currently require a Linux Agent-Up Server host.",
                "Application 'Web' failed to start."
            }));
            Assert.That(WorkspaceStartEvaluation.Summarize(failures), Does.Contain("Api: not enabled"));
            Assert.That(WorkspaceStartEvaluation.Summarize([]), Is.Null);
        });
    }

    private static ApplicationInstance Running(string name)
        => new() { Name = name, State = ApplicationState.Running };

    private static ApplicationInstance Failed(
        string name,
        ApplicationKind kind = ApplicationKind.Process,
        CapabilityStatusDto? capability = null)
        => new()
        {
            Name = name,
            Kind = kind,
            State = ApplicationState.Failed,
            CapabilityStatus = capability
        };
}
