using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Providers;
using AgentUp.Server.Features.Validation.Repositories;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

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
            var workspace = await registry.RegisterAsync(ServerDomain.Workspace().Named("Workspace").At(worktree).AtCommit("abc").Build());
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
            var json = await File.ReadAllTextAsync(filePath);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(saved.Id, Is.EqualTo(flow.Id));
                Assert.That(saved.WorkspaceId, Is.EqualTo(workspace.Id));
                Assert.That(saved.InitialPath, Is.EqualTo("/"));
                Assert.That(saved.Steps.Single().Description, Is.EqualTo("Open settings"));
                Assert.That(saved.Surface, Is.EqualTo(ValidationSurface.Application));
                Assert.That(json, Does.Not.Contain("surface"));
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

    [Test]
    public async Task Load_defaultsAMissingSurface_toApplication()
    {
        var worktree = Path.Join(Path.GetTempPath(), "agent-up-validation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(worktree, ".agent-up"));
        try
        {
            var registry = ServerTestComposition.CreateRegistry();
            var workspace = await registry.RegisterAsync(ServerDomain.Workspace().Named("Workspace").At(worktree).AtCommit("abc").Build());
            var filePath = Path.Join(worktree, ValidationFlowPathProvider.RelativeFlowFilePath);
            await File.WriteAllTextAsync(filePath,
                """
                {
                  "flows": [
                    {
                      "id": "legacy",
                      "application": "app",
                      "name": "Legacy",
                      "description": "Saved before surface existed",
                      "initialPath": "/",
                      "initialExpectations": [],
                      "steps": [],
                      "updatedAtUtc": "2026-01-01T00:00:00+00:00",
                      "version": 1
                    }
                  ]
                }
                """);
            var repository = new ProjectValidationFlowRepository(
                new ValidationFlowPathProvider(new WorkspaceQueryController(registry)));

            var loaded = await repository.LoadAsync(workspace.Id);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Single().Id, Is.EqualTo("legacy"));
                Assert.That(loaded.Single().Surface, Is.EqualTo(ValidationSurface.Application));
            });
        }
        finally
        {
            if (Directory.Exists(worktree))
                Directory.Delete(worktree, true);
        }
    }

    [Test]
    public async Task Save_persistsDesktopSurface()
    {
        var worktree = Path.Join(Path.GetTempPath(), "agent-up-validation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktree);
        try
        {
            var registry = ServerTestComposition.CreateRegistry();
            var workspace = await registry.RegisterAsync(ServerDomain.Workspace().Named("Workspace").At(worktree).AtCommit("abc").Build());
            var repository = new ProjectValidationFlowRepository(
                new ValidationFlowPathProvider(new WorkspaceQueryController(registry)));
            var flow = new ValidationFlow(
                "id",
                workspace.Id,
                "Desktop",
                "Open Git",
                "The Git tab is visible.",
                "workspace/git",
                [],
                [new("open", "Open Git", ValidationAction.Click, new(TestId: "shell.tab.git"))],
                DateTimeOffset.UtcNow,
                Surface: ValidationSurface.Desktop);

            await repository.SaveAsync(workspace.Id, [flow]);
            var loaded = await repository.LoadAsync(workspace.Id);
            var json = await File.ReadAllTextAsync(Path.Join(worktree, ValidationFlowPathProvider.RelativeFlowFilePath));

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Single().Surface, Is.EqualTo(ValidationSurface.Desktop));
                Assert.That(json, Does.Contain("\"surface\": \"Desktop\""));
            });
        }
        finally
        {
            if (Directory.Exists(worktree))
                Directory.Delete(worktree, true);
        }
    }
}
