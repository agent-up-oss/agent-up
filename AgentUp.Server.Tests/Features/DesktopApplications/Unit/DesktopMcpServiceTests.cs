using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Features.DesktopApplications.Controller;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class DesktopMcpServiceTests
{
    [Test]
    public async Task Reports_missing_sessions_and_rejects_oversized_text()
    {
        var service = CreateService(out _, out _);

        var inspection = await service.InspectAsync("workspace", "Editor");
        var screenshot = await service.ScreenshotAsync("workspace", "Editor", CancellationToken.None);
        var fill = await service.FillAsync("workspace", "Editor", 1, 1, 1, new string('x', 501), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(inspection.Succeeded, Is.False);
            Assert.That(screenshot.IsError, Is.True);
            Assert.That(fill.Succeeded, Is.False);
        });
    }

    [Test]
    public async Task Inspects_controls_fills_and_screenshots_a_running_session()
    {
        var service = CreateService(out var controller, out var displays);
        var workspace = new Workspace
        {
            Id = "workspace", DisplayName = "Workspace", RepositoryPath = "/repo", WorktreePath = "/repo",
            Branch = "main", Commit = "abc"
        };
        var application = new ApplicationInstance
        {
            Name = "Editor", Command = "editor", Kind = ApplicationKind.Desktop,
            DesktopWidth = 800, DesktopHeight = 600
        };
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var generation = controller.Get(workspace.Id, application.Name)!.Generation;

        var inspection = await service.InspectAsync(workspace.Id, application.Name);
        var click = await service.ClickAsync(workspace.Id, application.Name, generation, 10, 20, 1, CancellationToken.None);
        var press = await service.PressAsync(workspace.Id, application.Name, generation, "Escape", CancellationToken.None);
        var fill = await service.FillAsync(workspace.Id, application.Name, generation, 30, 40, "a b", CancellationToken.None);
        var screenshot = await service.ScreenshotAsync(workspace.Id, application.Name, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(inspection.Succeeded, Is.True);
            Assert.That(click.Succeeded, Is.True);
            Assert.That(press.Succeeded, Is.True);
            Assert.That(fill.Succeeded, Is.True);
            Assert.That(screenshot.IsError, Is.Not.True);
            Assert.That(screenshot.Content, Has.Count.EqualTo(2));
            Assert.That(displays.PointerEvents, Has.Count.EqualTo(4));
            Assert.That(displays.KeyEvents.Select(item => item.Key),
                Is.EqualTo(new[] { "Escape", "Escape", "a", "a", "space", "space", "b", "b" }));
        });

        await controller.StopAsync(workspace.Id, application.Name, CancellationToken.None);
    }

    private static DesktopMcpService CreateService(
        out DesktopApplicationsController controller,
        out FakeDesktopDisplayProvider displays)
    {
        displays = new FakeDesktopDisplayProvider();
        controller = new DesktopApplicationsController(new DesktopSessionService(
            displays,
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));
        return new DesktopMcpService(controller, ServerTestComposition.CreateAuditController());
    }
}
