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
    private const int DesktopWidth = 800;
    private const int DesktopHeight = 600;
    private static readonly byte[] PngHeader = [137, 80, 78, 71];

    private static (DesktopApplicationsController Controller, FakeDesktopDisplayProvider Displays) CreateController()
    {
        var displays = new FakeDesktopDisplayProvider();
        return (new DesktopApplicationsController(new DesktopSessionService(
            displays,
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance)), displays);
    }

    private static Workspace Workspace() => new()
    {
        Id = "workspace",
        DisplayName = "Workspace",
        RepositoryPath = "/repo",
        WorktreePath = "/repo",
        Branch = "main",
        Commit = "abc"
    };

    private static ApplicationInstance Application() => new()
    {
        Name = "Editor",
        Command = "dotnet run",
        Kind = ApplicationKind.Desktop,
        DesktopWidth = DesktopWidth,
        DesktopHeight = DesktopHeight
    };

    private static string TicketValue(
        DesktopApplicationsController controller, Workspace workspace, ApplicationInstance application)
    {
        var ticket = controller.CreateViewerTicket(workspace.Id, application.Name)!;
        return new Uri("http://localhost" + ticket.ViewerUrl).Query["?ticket=".Length..];
    }

    // Every hosted desktop variable the application's process inherits. Stated as cases so
    // a dropped variable names itself instead of disappearing into a block of twenty-odd
    // assertions.
    [TestCase("DISPLAY", ":123")]
    [TestCase("XDG_RUNTIME_DIR", "/tmp/agentup-desktop-fake")]
    [TestCase("GDK_BACKEND", "x11")]
    [TestCase("WAYLAND_DISPLAY", "agentup-hosted-no-wayland")]
    [TestCase("XDG_SESSION_TYPE", "x11")]
    [TestCase("GTK_USE_PORTAL", "0")]
    [TestCase("QT_QPA_PLATFORM", "xcb")]
    [TestCase("LIBGL_ALWAYS_SOFTWARE", "1")]
    [TestCase("LD_LIBRARY_PATH", "/nix/store/fake-fontconfig/lib")]
    public async Task Prepare_putsTheHostedDisplayIntoTheApplicationEnvironment(string name, string expected)
    {
        var (controller, _) = CreateController();

        var environment = await controller.PrepareAsync(Workspace(), Application(), CancellationToken.None);

        Assert.That(environment[name], Is.EqualTo(expected));
    }

    [Test]
    public async Task Prepare_opensAGenerationScopedSessionSizedToTheApplication()
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();

        await controller.PrepareAsync(workspace, application, CancellationToken.None);

        var session = controller.Get(workspace.Id, application.Name);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.Generation, Is.GreaterThan(0));
        Assert.That(session.Width, Is.EqualTo(DesktopWidth));
    }

    [Test]
    public async Task ViewerTicket_addressesTheSessionItWasIssuedFor()
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name)!;

        var ticket = controller.CreateViewerTicket(workspace.Id, application.Name);

        Assert.That(ticket!.ViewerUrl, Does.Contain(session.SessionId));
        Assert.That(controller.GetBySessionId(session.SessionId), Is.Not.Null);
    }

    [Test]
    public async Task ViewerPage_isServedOnlyToTheTicketThatOpenedTheSession()
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name)!;
        var ticket = TicketValue(controller, workspace, application);

        Assert.That(controller.CreateViewerPage(session.SessionId, ticket), Does.Contain("pointerdown"));
        Assert.That(controller.CreateViewerPage(session.SessionId, "wrong"), Is.Null);
    }

    // The stream is a WebSocket upgrade behind a one-shot ticket, so the three answers are
    // "not an upgrade", "not your ticket", and "go ahead".
    [TestCase(false, true, 400)]
    [TestCase(true, false, 401)]
    [TestCase(true, true, 101)]
    public async Task StreamRequest_isValidatedAgainstTheUpgradeAndTheTicket(
        bool isWebSocketRequest, bool correctTicket, int expected)
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name)!;
        var ticket = correctTicket ? TicketValue(controller, workspace, application) : "wrong";

        Assert.That(
            controller.ValidateStreamRequest(session.SessionId, ticket, isWebSocketRequest),
            Is.EqualTo(expected));
    }

    [Test]
    public async Task Capture_returnsThePngTheDisplayProduced()
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name)!;

        var screenshot = await controller.CaptureAsync(
            workspace.Id, application.Name, session.Generation, CancellationToken.None);

        Assert.That(screenshot, Is.EqualTo(PngHeader));
    }

    [Test]
    public async Task PointerAndKeyEvents_reachTheHostedDisplay()
    {
        var (controller, displays) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var session = controller.Get(workspace.Id, application.Name)!;

        await controller.PointerAsync(
            workspace.Id, application.Name,
            new DesktopPointerRequest(session.Generation, 10, 20, 1), true, CancellationToken.None);
        await controller.KeyAsync(
            workspace.Id, application.Name,
            new DesktopKeyRequest(session.Generation, "Escape", true), CancellationToken.None);

        Assert.That(displays.PointerEvents, Has.Count.EqualTo(1));
        Assert.That(displays.KeyEvents, Has.Count.EqualTo(1));
    }

    // Input carries the generation it was aimed at, so a viewer left open across a restart
    // cannot drive the new session with the old one's coordinates.
    [Test]
    public async Task Input_fromAPreviousGeneration_isRefused()
    {
        var (controller, _) = CreateController();
        var workspace = Workspace();
        var application = Application();
        await controller.PrepareAsync(workspace, application, CancellationToken.None);
        var stale = controller.Get(workspace.Id, application.Name)!.Generation;
        await controller.PrepareAsync(workspace, application, CancellationToken.None);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.PointerAsync(
                workspace.Id,
                application.Name,
                new DesktopPointerRequest(stale, 10, 10),
                true,
                CancellationToken.None));
    }

    [Test]
    public async Task StopWorkspace_stopsTheHostedDisplay()
    {
        var (controller, displays) = CreateController();
        await controller.PrepareAsync(Workspace(), Application(), CancellationToken.None);

        await controller.StopWorkspaceAsync(Workspace().Id, CancellationToken.None);

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
