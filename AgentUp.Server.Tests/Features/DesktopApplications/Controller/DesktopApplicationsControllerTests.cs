using System.Diagnostics;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Interfaces;
using AgentUp.Server.Features.DesktopApplications.Models;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Controller;

[TestFixture]
public sealed class DesktopApplicationsControllerTests
{
    [Test]
    public async Task Prepares_a_generation_scoped_session_and_issues_a_viewer()
    {
        var displays = new FakeDesktopDisplayProvider();
        var remote = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var controller = new DesktopApplicationsController(new DesktopSessionService(
            displays,
            remote,
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            NullLogger<DesktopSessionService>.Instance));
        var workspace = new Workspace
        {
            Id = "workspace", DisplayName = "Workspace", RepositoryPath = "/repo", WorktreePath = "/repo",
            Branch = "main", Commit = "abc"
        };
        var application = new ApplicationInstance
        {
            Name = "Editor", Command = "dotnet run", Kind = ApplicationKind.Desktop,
            DesktopWidth = 800, DesktopHeight = 600
        };

        var environment = await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name);
        var ticket = controller.CreateViewerTicket(workspace.Id, application.Name);
        var ticketValue = new Uri("http://localhost" + ticket!.ViewerUrl).Query["?ticket=".Length..];
        var viewerPage = controller.CreateViewerPage(session!.SessionId, ticketValue);

        Assert.Multiple(() =>
        {
            Assert.That(environment["DISPLAY"], Is.EqualTo(":123"));
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Generation, Is.GreaterThan(0));
            Assert.That(session.Width, Is.EqualTo(800));
            Assert.That(ticket.ViewerUrl, Does.Contain(session.SessionId));
            Assert.That(viewerPage, Does.Contain("pointerdown"));
            Assert.That(controller.CreateViewerPage(session.SessionId, "wrong"), Is.Null);
        });

        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.PointerAsync(
                workspace.Id,
                application.Name,
                new DesktopPointerRequest(session.Generation, 10, 10),
                true,
                CancellationToken.None));

        await controller.StopWorkspaceAsync(workspace.Id, CancellationToken.None);
        Assert.That(displays.Stopped, Is.True);
    }
}

internal sealed class FakeDesktopDisplayProvider : IDesktopDisplayProvider
{
    public bool Stopped { get; private set; }

    public Task<DesktopDisplayHandle> StartAsync(int width, int height, CancellationToken cancellationToken) =>
        Task.FromResult(new DesktopDisplayHandle(":123", Process.GetCurrentProcess(), width, height));

    public Task<byte[]> CapturePngAsync(DesktopDisplayHandle display, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]>([137, 80, 78, 71]);

    public Task SendPointerAsync(DesktopDisplayHandle display, int x, int y, int button, bool pressed, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task SendKeyAsync(DesktopDisplayHandle display, string key, bool pressed, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StopAsync(DesktopDisplayHandle display, CancellationToken cancellationToken)
    {
        Stopped = true;
        display.DisplayProcess.Dispose();
        return Task.CompletedTask;
    }
}
