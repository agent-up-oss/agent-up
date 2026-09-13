using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class DesktopSessionServiceTests
{
    [Test]
    public async Task Prepare_returnsEmptyEnvironmentForNonDesktopApplications()
    {
        var service = CreateService(out _);
        var environment = await service.PrepareAsync(
            Workspace(),
            new ApplicationInstance { Name = "Web", Command = "npm start", Kind = ApplicationKind.Process },
            CancellationToken.None);

        Assert.That(environment, Is.Empty);
        Assert.That(service.Get("workspace", "Web"), Is.Null);
    }

    [Test]
    public async Task DispatchInput_sendsPointerAndKeyEventsAndIgnoresInvalidJson()
    {
        var service = CreateService(out var displays);
        var workspace = Workspace();
        var application = DesktopApplication();
        await service.PrepareAsync(workspace, application, CancellationToken.None);
        var session = service.GetBySessionId(service.Get(workspace.Id, application.Name)!.SessionId)!;

        await service.DispatchInputMessageAsync(session, "{", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"pointerMove","x":1,"y":2}""", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"pointerDown","x":3,"y":4,"button":1}""", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"pointerUp","x":3,"y":4,"button":1}""", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"keyDown","key":"a"}""", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"keyUp","key":"a"}""", CancellationToken.None);
        await service.DispatchInputMessageAsync(session, """{"type":"keyDown"}""", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(displays.PointerEvents, Is.EqualTo(new[] { (1, 2, -1, false), (3, 4, 1, true), (3, 4, 1, false) }));
            Assert.That(displays.KeyEvents, Is.EqualTo(new[] { ("a", true), ("a", false) }));
        });

        await service.StopAsync(workspace.Id, application.Name, CancellationToken.None);
    }

    [Test]
    public async Task CaptureLoop_capturesFramesWhenAPollingViewerIsPresent()
    {
        var remote = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var service = CreateService(out var displays, remote);
        var workspace = Workspace();
        var application = DesktopApplication();
        await service.PrepareAsync(workspace, application, CancellationToken.None);
        var session = service.Get(workspace.Id, application.Name)!;
        remote.RegisterPollingViewer(session.SessionId);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (displays.CaptureCalls == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.That(displays.CaptureCalls, Is.GreaterThan(0));
        await service.StopAsync(workspace.Id, application.Name, CancellationToken.None);
    }

    [Test]
    public async Task CaptureLoop_marksTheSessionDegradedWhenCaptureFails()
    {
        var remote = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var service = CreateService(out var displays, remote);
        displays.CaptureError = new InvalidOperationException("framebuffer gone");
        var workspace = Workspace();
        var application = DesktopApplication();
        await service.PrepareAsync(workspace, application, CancellationToken.None);
        var session = service.Get(workspace.Id, application.Name)!;
        remote.RegisterPollingViewer(session.SessionId);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (service.Get(workspace.Id, application.Name)?.State != "degraded" && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.That(service.Get(workspace.Id, application.Name)?.State, Is.EqualTo("degraded"));
        await service.StopAsync(workspace.Id, application.Name, CancellationToken.None);
    }

    private static DesktopSessionService CreateService(
        out FakeDesktopDisplayProvider displays,
        BrowserRemoteDisplayService? remote = null)
    {
        displays = new FakeDesktopDisplayProvider();
        return new DesktopSessionService(
            displays,
            remote ?? new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance);
    }

    private static Workspace Workspace() => new()
    {
        Id = "workspace", DisplayName = "Workspace", RepositoryPath = "/repo", WorktreePath = "/repo",
        Branch = "main", Commit = "abc"
    };

    private static ApplicationInstance DesktopApplication() => new()
    {
        Name = "Editor", Command = "dotnet run", Kind = ApplicationKind.Desktop,
        DesktopWidth = 800, DesktopHeight = 600
    };
}
