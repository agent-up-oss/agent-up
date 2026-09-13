namespace AgentUp.Server.Features.DesktopApplications.DTOs;

public sealed record DesktopSessionDto(
    string WorkspaceId,
    string Application,
    string SessionId,
    long Generation,
    string State,
    int Width,
    int Height,
    string? ViewerPath,
    string? Error);

public sealed record DesktopViewerTicketDto(string ViewerUrl, DateTimeOffset ExpiresAtUtc);

public sealed record DesktopPointerRequest(long Generation, int X, int Y, int Button = 0);

public sealed record DesktopKeyRequest(long Generation, string Key, bool Pressed = true);
