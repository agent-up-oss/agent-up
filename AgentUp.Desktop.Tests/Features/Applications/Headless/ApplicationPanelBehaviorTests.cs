using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Applications.Headless;

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
    public async Task Panel_resetsApplicationSelection_whenWorkspaceSwitched()
    {
        var workspaces = new List<AgentUp.Desktop.Features.Workspaces.DTOs.WorkspaceDto>
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
