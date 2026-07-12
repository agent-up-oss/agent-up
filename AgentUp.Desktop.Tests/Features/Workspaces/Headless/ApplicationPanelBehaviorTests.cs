using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class ApplicationPanelBehaviorTests
{
    [AvaloniaTest]
    public async Task Panel_autoSelectsFirstApplication_whenWorkspaceHasApplications()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);

        Assert.That(app.Content.HasApplications, Is.True);
        Assert.That(app.Content.SelectedApplicationName, Is.EqualTo(workspace.Applications[0].Name));
    }

    [AvaloniaTest]
    public async Task Panel_showsNoApplicationsMessage_whenNoneConfigured()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());

        Assert.That(app.Content.HasApplications, Is.False);
        Assert.That(app.Content.SelectedApplicationName, Is.Null);
    }

    [AvaloniaTest]
    public async Task Panel_loadsConsoleOutput_forAutoSelectedApplication()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var outputLines = new List<string> { "$ cargo run", "▸ Listening on :3001", "› GET / 200" };
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, outputLines);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleLines, Has.Count.EqualTo(outputLines.Count));
        Assert.That(app.Content.ConsoleLines[0], Is.EqualTo(outputLines[0]));
    }

    [AvaloniaTest]
    public async Task Panel_updatesConsoleOutput_whenDifferentApplicationSelected()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var output = new Dictionary<string, List<string>>
        {
            [$"{workspace.Id}/{workspace.Applications[0].Name}"] = ["line from API"],
            [$"{workspace.Id}/{workspace.Applications[1].Name}"] = ["line from Docs"],
        };

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);
        await app.Content.SelectApplicationByIndexAsync(1);

        Assert.That(app.Content.SelectedApplicationName, Is.EqualTo(workspace.Applications[1].Name));
        Assert.That(app.Content.ConsoleLines, Has.Some.EqualTo("line from Docs"));
    }

    [AvaloniaTest]
    public async Task Panel_resetsApplicationSelection_whenWorkspaceSwitched()
    {
        var workspaces = new List<AgentUp.Desktop.Features.Workspaces.Http.WorkspaceDto>
        {
            WorkspaceFixtures.WithApplications(),
            WorkspaceFixtures.Single() with { Id = "ws-2", DisplayName = "Other" },
        };
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        await app.Sidebar.SelectWorkspaceAtIndexAsync(1);

        Assert.That(app.Content.HasApplications, Is.False);
        Assert.That(app.Content.SelectedApplicationName, Is.Null);
    }
}
