using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Models;
using AgentUp.CLI.Features.Workspaces.Services;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceServicesTests
{
    [Test]
    public void Configuration_result_requires_both_configuration_and_root()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new WorkspaceConfigurationResult(new AgentUpJson("sample"), "/repo", null).Succeeded, Is.True);
            Assert.That(new WorkspaceConfigurationResult(new AgentUpJson("sample"), null, "missing").Succeeded, Is.False);
            Assert.That(new WorkspaceConfigurationResult(null, "/repo", "invalid").Succeeded, Is.False);
        });
    }

    [Test]
    public void Workspace_identity_preserves_git_coordinates()
    {
        var identity = new WorkspaceIdentity("/repo", "feature/ports", "abc123");

        Assert.That((identity.RepositoryPath, identity.Branch, identity.Commit),
            Is.EqualTo(("/repo", "feature/ports", "abc123")));
    }

    [Test]
    public void Resolution_distinguishes_found_workspaces_from_failures()
    {
        var workspace = CliDomain.Workspace()
            .WithId("one")
            .Named("One")
            .WithRepositoryPath("/repo")
            .WithWorktreePath("/repo")
            .OnBranch("main")
            .AtCommit("abc")
            .InState("running")
            .Build();

        var found = WorkspaceResolution.Found(workspace);
        var failed = WorkspaceResolution.Failed("not registered");

        Assert.Multiple(() =>
        {
            Assert.That(found.Succeeded, Is.True);
            Assert.That(found.Workspace, Is.SameAs(workspace));
            Assert.That(found.Error, Is.Null);
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Workspace, Is.Null);
            Assert.That(failed.Error, Is.EqualTo("not registered"));
        });
    }

    [Test]
    public void Output_service_reports_an_empty_workspace_list_as_success()
    {
        var output = new StringWriter();
        var service = new WorkspaceCommandOutputService(output);

        var exitCode = service.WriteListResult(WorkspaceCommandResult<IReadOnlyList<WorkspaceDto>>.Success([]));

        Assert.That(exitCode, Is.Zero);
        Assert.That(output.ToString(), Does.Contain("No workspaces registered."));
    }
}
