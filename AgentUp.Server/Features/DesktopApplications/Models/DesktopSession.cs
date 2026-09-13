using AgentUp.Server.Features.DesktopApplications.DTOs;

namespace AgentUp.Server.Features.DesktopApplications.Models;

public sealed class DesktopSession
{
    public required string WorkspaceId { get; init; }
    public required string Application { get; init; }
    public required string SessionId { get; init; }
    public required long Generation { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required DesktopDisplayHandle Display { get; init; }
    public required CancellationTokenSource Lifetime { get; init; }
    public Task? CaptureLoop { get; set; }
    public string? Error { get; set; }

    public DesktopSessionDto ToDto(string state, string? viewerPath = null) =>
        new(WorkspaceId, Application, SessionId, Generation, state, Width, Height, viewerPath, Error);
}
