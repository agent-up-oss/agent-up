using System.Net.WebSockets;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.DesktopApplications.Controllers;

public sealed class DesktopApplicationsController(DesktopSessionService sessions)
{
    public Task<IReadOnlyDictionary<string, string>> PrepareAsync(Workspace workspace, ApplicationInstance application, CancellationToken cancellationToken) =>
        sessions.PrepareAsync(workspace, application, cancellationToken);

    public DesktopSessionDto? Get(string workspaceId, string application) => sessions.Get(workspaceId, application);

    public DesktopSessionDto? GetBySessionId(string sessionId) => sessions.GetDtoBySessionId(sessionId);

    public DesktopViewerTicketDto? CreateViewerTicket(string workspaceId, string application) =>
        sessions.CreateViewerTicket(workspaceId, application);

    public string? CreateViewerPage(string sessionId, string? ticket) => sessions.CreateViewerPage(sessionId, ticket);

    public int ValidateStreamRequest(string sessionId, string? ticket, bool isWebSocket) =>
        sessions.ValidateStreamRequest(sessionId, ticket, isWebSocket);

    public Task StreamAsync(string sessionId, WebSocket socket, CancellationToken cancellationToken) =>
        sessions.StreamAsync(sessionId, socket, cancellationToken);

    public Task<byte[]> CaptureAsync(string workspaceId, string application, long? generation, CancellationToken cancellationToken) =>
        sessions.CaptureAsync(workspaceId, application, generation, cancellationToken);

    public Task PointerAsync(string workspaceId, string application, DesktopPointerRequest request, bool pressed, CancellationToken cancellationToken) =>
        sessions.PointerAsync(workspaceId, application, request, pressed, cancellationToken);

    public Task KeyAsync(string workspaceId, string application, DesktopKeyRequest request, CancellationToken cancellationToken) =>
        sessions.KeyAsync(workspaceId, application, request, cancellationToken);

    public Task StopWorkspaceAsync(string workspaceId, CancellationToken cancellationToken) =>
        sessions.StopWorkspaceAsync(workspaceId, cancellationToken);

    public Task StopAsync(string workspaceId, string application, CancellationToken cancellationToken) =>
        sessions.StopAsync(workspaceId, application, cancellationToken);
}
