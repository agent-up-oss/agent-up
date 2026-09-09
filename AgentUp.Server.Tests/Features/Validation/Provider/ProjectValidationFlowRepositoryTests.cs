using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Providers;
using AgentUp.Server.Features.Validation.Repositories;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Validation.Provider;

public sealed class ProjectValidationFlowRepositoryTests
{
    [Test]
    public async Task Save_and_load_persist_flows_under_agent_up_directory()
    {
        var worktree = Path.Join(Path.GetTempPath(), "agent-up-validation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktree);
        try
        {
            var registry = ServerTestComposition.CreateRegistry();
            var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Workspace", worktree, worktree, "main", "abc"));
            var paths = new ValidationFlowPathProvider(new WorkspaceQueryController(registry));
            var repository = new ProjectValidationFlowRepository(paths);
            var flow = new ValidationFlow(
                "id",
                workspace.Id,
                "app",
                "Name",
                "Description",
                "/",
                [],
                [new("step", "Open settings", ValidationAction.Click, new(Text: "Settings"))],
                DateTimeOffset.UtcNow);

            await repository.SaveAsync(workspace.Id, [flow]);
            var loaded = await repository.LoadAsync(workspace.Id);
            var saved = loaded.Single();
            var filePath = Path.Join(worktree, ValidationFlowPathProvider.RelativeFlowFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(saved.Id, Is.EqualTo(flow.Id));
                Assert.That(saved.WorkspaceId, Is.EqualTo(workspace.Id));
                Assert.That(saved.InitialPath, Is.EqualTo("/"));
                Assert.That(saved.Steps.Single().Description, Is.EqualTo("Open settings"));
            });
        }
        finally
        {
            if (Directory.Exists(worktree))
                Directory.Delete(worktree, true);
        }
    }

    [Test]
    public void GetFlowFilePath_rejects_paths_outside_the_worktree()
    {
        var root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "agent-up-validation-root"));
        var outside = Path.GetFullPath(Path.Join(root, "..", "outside", "validation-flows.json"));

        Assert.That(ValidationFlowPathProvider.IsUnderRoot(root, outside), Is.False);
    }
}
