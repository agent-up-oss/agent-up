using System.Net.WebSockets;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
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
            new FakeHostedDesktopNativeLibraryProvider(),
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
        var screenshot = await controller.CaptureAsync(workspace.Id, application.Name, session.Generation, CancellationToken.None);
        using var socket = new ClosingWebSocket();
        await controller.StreamAsync(session.SessionId, socket, CancellationToken.None);
        await controller.PointerAsync(workspace.Id, application.Name, new DesktopPointerRequest(session.Generation, 10, 20, 1), true, CancellationToken.None);
        await controller.KeyAsync(workspace.Id, application.Name, new DesktopKeyRequest(session.Generation, "Escape", true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(environment["DISPLAY"], Is.EqualTo(":123"));
            Assert.That(environment["XDG_RUNTIME_DIR"], Is.EqualTo("/tmp/agentup-desktop-fake"));
            Assert.That(environment["GDK_BACKEND"], Is.EqualTo("x11"));
            Assert.That(environment["WAYLAND_DISPLAY"], Is.EqualTo("agentup-hosted-no-wayland"));
            Assert.That(environment["XDG_SESSION_TYPE"], Is.EqualTo("x11"));
            Assert.That(environment["GTK_USE_PORTAL"], Is.EqualTo("0"));
            Assert.That(environment["QT_QPA_PLATFORM"], Is.EqualTo("xcb"));
            Assert.That(environment["LIBGL_ALWAYS_SOFTWARE"], Is.EqualTo("1"));
            Assert.That(environment["LD_LIBRARY_PATH"], Is.EqualTo("/nix/store/fake-fontconfig/lib"));
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Generation, Is.GreaterThan(0));
            Assert.That(session.Width, Is.EqualTo(800));
            Assert.That(ticket.ViewerUrl, Does.Contain(session.SessionId));
            Assert.That(viewerPage, Does.Contain("pointerdown"));
            Assert.That(controller.CreateViewerPage(session.SessionId, "wrong"), Is.Null);
            Assert.That(controller.GetBySessionId(session.SessionId), Is.Not.Null);
            Assert.That(controller.ValidateStreamRequest(session.SessionId, ticketValue, false), Is.EqualTo(400));
            Assert.That(controller.ValidateStreamRequest(session.SessionId, "wrong", true), Is.EqualTo(401));
            Assert.That(controller.ValidateStreamRequest(session.SessionId, ticketValue, true), Is.EqualTo(101));
            Assert.That(screenshot, Is.EqualTo(new byte[] { 137, 80, 78, 71 }));
            Assert.That(displays.PointerEvents, Has.Count.EqualTo(1));
            Assert.That(displays.KeyEvents, Has.Count.EqualTo(1));
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

    [Test]
    public void Get_returnsNullWhenTheWorkspaceHasNoDesktopSession()
    {
        var controller = new DesktopApplicationsController(new DesktopSessionService(
            new FakeDesktopDisplayProvider(),
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));

        Assert.Multiple(() =>
        {
            Assert.That(controller.Get("missing", "Editor"), Is.Null);
            Assert.That(controller.CreateViewerTicket("missing", "Editor"), Is.Null);
            Assert.That(controller.GetBySessionId("missing"), Is.Null);
        });
        using var socket = new ClosingWebSocket();
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.StreamAsync("missing", socket, CancellationToken.None));
    }
}

internal sealed class ClosingWebSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;
    public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;
    public override void Abort() => _state = WebSocketState.Aborted;
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        CloseAsync(closeStatus, statusDescription, cancellationToken);
    public override void Dispose() => _state = WebSocketState.Closed;
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
        Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
