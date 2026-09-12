using System.Collections.Concurrent;
using System.Net.WebSockets;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Interfaces;
using AgentUp.Server.Features.DesktopApplications.Models;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.DesktopApplications.Services;

public sealed class DesktopSessionService(
    IDesktopDisplayProvider displays,
    BrowserRemoteDisplayService remoteDisplay,
    DesktopInputMessageProvider inputMessages,
    DesktopViewerTicketProvider tickets,
    ILogger<DesktopSessionService> logger) : IHostedService
{
    private readonly ConcurrentDictionary<(string WorkspaceId, string Application), DesktopSession> _sessions = new();
    private long _generation;

    public async Task<IReadOnlyDictionary<string, string>> PrepareAsync(
        Workspace workspace,
        ApplicationInstance application,
        CancellationToken cancellationToken)
    {
        if (application.Kind != ApplicationKind.Desktop)
            return new Dictionary<string, string>();

        await StopAsync(workspace.Id, application.Name, cancellationToken);
        var display = await displays.StartAsync(application.DesktopWidth, application.DesktopHeight, cancellationToken);
        var sessionId = Guid.NewGuid().ToString("N");
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var session = new DesktopSession
        {
            WorkspaceId = workspace.Id,
            Application = application.Name,
            SessionId = sessionId,
            Generation = Interlocked.Increment(ref _generation),
            Width = application.DesktopWidth,
            Height = application.DesktopHeight,
            Display = display,
            Lifetime = lifetime
        };
        _sessions[(workspace.Id, application.Name)] = session;
        session.CaptureLoop = RunCaptureLoopAsync(session);
        return new Dictionary<string, string>
        {
            ["DISPLAY"] = display.DisplayName,
            ["NO_AT_BRIDGE"] = "0"
        };
    }

    public DesktopSessionDto? Get(string workspaceId, string application)
    {
        var session = Find(workspaceId, application);
        return session?.ToDto(
            session.Error is null ? "ready" : "degraded",
            $"/api/desktop-applications/{Uri.EscapeDataString(workspaceId)}/{Uri.EscapeDataString(application)}/viewer");
    }

    public DesktopSession? GetBySessionId(string sessionId) =>
        _sessions.Values.FirstOrDefault(session =>
            string.Equals(session.SessionId, sessionId, StringComparison.Ordinal));

    public DesktopSessionDto? GetDtoBySessionId(string sessionId) =>
        GetBySessionId(sessionId)?.ToDto("ready");

    public DesktopViewerTicketDto? CreateViewerTicket(string workspaceId, string application)
    {
        var session = Get(workspaceId, application);
        if (session is null) return null;
        var issued = tickets.Issue(session.SessionId);
        return new DesktopViewerTicketDto(
            $"/api/desktop-applications/session/{session.SessionId}/viewer?ticket={issued.Ticket}",
            issued.ExpiresAtUtc);
    }

    public string? CreateViewerPage(string sessionId, string? ticket)
    {
        var session = GetBySessionId(sessionId);
        if (session is null || !tickets.Validate(sessionId, ticket)) return null;
        var socketPath = $"/api/desktop-applications/session/{sessionId}/display?ticket={Uri.EscapeDataString(ticket!)}";
        return AgentUp.Browser.Streaming.Resources.RemoteDisplayViewerPage.Build(
            new AgentUp.Browser.Streaming.DTOs.RemoteDisplayViewerOptions(
                session.Application, socketPath, "image/png", session.Width, session.Height));
    }

    public int ValidateStreamRequest(string sessionId, string? ticket, bool isWebSocket) =>
        !tickets.Validate(sessionId, ticket)
            ? StatusCodes.Status401Unauthorized
            : !isWebSocket
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status101SwitchingProtocols;

    public async Task StreamAsync(string sessionId, WebSocket socket, CancellationToken cancellationToken)
    {
        var session = GetBySessionId(sessionId)
            ?? throw new InvalidOperationException("Desktop session was not found.");
        await remoteDisplay.ConnectAsync(
            session.SessionId,
            socket,
            json => DispatchInputMessageAsync(session, json, cancellationToken),
            cancellationToken);
    }

    public async Task<byte[]> CaptureAsync(
        string workspaceId,
        string application,
        long? generation,
        CancellationToken cancellationToken)
    {
        var session = FindRequired(workspaceId, application, generation);
        return await displays.CapturePngAsync(session.Display, cancellationToken);
    }

    public async Task PointerAsync(
        string workspaceId,
        string application,
        DesktopPointerRequest request,
        bool pressed,
        CancellationToken cancellationToken)
    {
        var session = FindRequired(workspaceId, application, request.Generation);
        await displays.SendPointerAsync(session.Display, request.X, request.Y, request.Button, pressed, cancellationToken);
    }

    public async Task KeyAsync(
        string workspaceId,
        string application,
        DesktopKeyRequest request,
        CancellationToken cancellationToken)
    {
        var session = FindRequired(workspaceId, application, request.Generation);
        await displays.SendKeyAsync(session.Display, request.Key, request.Pressed, cancellationToken);
    }

    public async Task StopWorkspaceAsync(string workspaceId, CancellationToken cancellationToken)
    {
        foreach (var application in _sessions.Keys.Where(key => key.WorkspaceId == workspaceId).Select(key => key.Application).ToList())
            await StopAsync(workspaceId, application, cancellationToken);
    }

    public async Task StopAsync(string workspaceId, string application, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove((workspaceId, application), out var session))
            return;
        await session.Lifetime.CancelAsync();
        tickets.RevokeSession(session.SessionId);
        await remoteDisplay.DisconnectAllAsync(session.SessionId, cancellationToken);
        if (session.CaptureLoop is not null)
        {
            try { await session.CaptureLoop; }
            catch (OperationCanceledException) when (session.Lifetime.IsCancellationRequested)
            {
                logger.LogDebug("Desktop capture loop stopped for session {SessionId}", session.SessionId);
            }
        }
        await displays.StopAsync(session.Display, cancellationToken);
        session.Lifetime.Dispose();
    }

    private async Task RunCaptureLoopAsync(DesktopSession session)
    {
        while (!session.Lifetime.IsCancellationRequested)
        {
            try
            {
                if (remoteDisplay.HasSubscribers(session.SessionId))
                {
                    var frame = await displays.CapturePngAsync(session.Display, session.Lifetime.Token);
                    await remoteDisplay.BroadcastFrameAsync(session.SessionId, frame, session.Lifetime.Token);
                    session.Error = null;
                }
                await Task.Delay(remoteDisplay.HasForegroundSubscribers(session.SessionId) ? 100 : 500, session.Lifetime.Token);
            }
            catch (OperationCanceledException) when (session.Lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Runtime.InteropServices.ExternalException)
            {
                session.Error = "Desktop framebuffer capture failed.";
                logger.LogWarning(ex, "Desktop framebuffer capture failed for session {SessionId}", session.SessionId);
                await Task.Delay(500, session.Lifetime.Token);
            }
        }
    }

    private async Task DispatchInputMessageAsync(DesktopSession session, string json, CancellationToken cancellationToken)
    {
        var message = inputMessages.Parse(json);
        if (message is null) return;
        switch (message.Type)
        {
            case "pointerMove":
                await displays.SendPointerAsync(session.Display, message.X, message.Y, -1, false, cancellationToken);
                break;
            case "pointerDown":
                await displays.SendPointerAsync(session.Display, message.X, message.Y, message.Button, true, cancellationToken);
                break;
            case "pointerUp":
                await displays.SendPointerAsync(session.Display, message.X, message.Y, message.Button, false, cancellationToken);
                break;
            case "keyDown" when message.Key is not null:
                await displays.SendKeyAsync(session.Display, message.Key, true, cancellationToken);
                break;
            case "keyUp" when message.Key is not null:
                await displays.SendKeyAsync(session.Display, message.Key, false, cancellationToken);
                break;
        }
    }

    private DesktopSession? Find(string workspaceId, string application) =>
        _sessions.GetValueOrDefault((workspaceId, application));

    private DesktopSession FindRequired(string workspaceId, string application, long? generation)
    {
        var session = Find(workspaceId, application)
            ?? throw new InvalidOperationException("Desktop application session is not running.");
        if (generation.HasValue && generation.Value != session.Generation)
            throw new InvalidOperationException("Desktop application session changed; inspect it again before sending input.");
        return session;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var workspaceId in _sessions.Keys.Select(key => key.WorkspaceId).Distinct().ToList())
            await StopWorkspaceAsync(workspaceId, cancellationToken);
    }
}
