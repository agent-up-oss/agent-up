namespace AgentUp.Desktop.Features.Workspaces.DTOs;

public sealed record DesktopViewerTicketResponse(string ViewerUrl, DateTimeOffset ExpiresAtUtc);
