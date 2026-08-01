namespace AgentUp.Server.Features.Audit.Models;

public sealed record AuditScreenshot(
    string WorkspaceId,
    string Url,
    string MimeType,
    string ImageBase64,
    int Width,
    int Height);
