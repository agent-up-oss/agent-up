using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;

namespace AgentUp.Server.Features.Audit.Controllers;

public sealed class AuditController(AuditService audit)
{
    public Task<AuditEvent> RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken)
        => audit.RecordAsync(request, cancellationToken);

    public Task<(AuditEvent Event, AuditArtifact Artifact)> RecordScreenshotAsync(
        AuditScreenshot screenshot,
        CancellationToken cancellationToken)
        => audit.RecordScreenshotAsync(screenshot, cancellationToken);

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
        => audit.QueryAsync(query, cancellationToken);

    public Task<AuditEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken)
        => audit.GetEventAsync(eventId, cancellationToken);

    public Task<AuditArtifactResult?> LoadArtifactAsync(
        string artifactId,
        bool includeImage,
        CancellationToken cancellationToken)
        => audit.LoadArtifactAsync(artifactId, includeImage, cancellationToken);
}
