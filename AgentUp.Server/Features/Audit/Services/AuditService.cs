using System.Text.Json;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Audit.Services;

public sealed class AuditService(
    IAuditEventRepository events,
    IAuditArtifactRepository artifacts,
    IAuditIdentityProvider identities,
    WorkspaceQueryController workspaces)
{
    public async Task<AuditEvent> RecordAsync(
        AuditRecordRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(request.WorkspaceId, cancellationToken);
        var evt = CreateEvent(request, identity, request.ArtifactIds ?? []);
        await events.AppendAsync(evt, cancellationToken);
        return evt;
    }

    public async Task<(AuditEvent Event, AuditArtifact Artifact)> RecordScreenshotAsync(
        AuditScreenshot screenshot,
        CancellationToken cancellationToken)
    {
        var bytes = Convert.FromBase64String(screenshot.ImageBase64);
        var eventId = Guid.NewGuid().ToString("N");
        var artifact = await artifacts.SaveAsync(eventId, "browser-screenshot", screenshot.MimeType, bytes, cancellationToken);
        var identity = await ResolveIdentityAsync(screenshot.WorkspaceId, cancellationToken);
        var evt = new AuditEvent(
            eventId,
            DateTimeOffset.UtcNow,
            "browser",
            "mcp",
            "browser_screenshot",
            "success",
            screenshot.WorkspaceId,
            identity.RepositoryPath,
            identity.WorktreePath,
            identity.WorkdirId,
            identity.Branch,
            identity.Commit,
            identity.Dirty,
            new Dictionary<string, string>
            {
                ["url"] = screenshot.Url,
                ["mimeType"] = screenshot.MimeType,
                ["width"] = screenshot.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["height"] = screenshot.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["sizeBytes"] = bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            [artifact.ArtifactId]);
        await events.AppendAsync(evt, cancellationToken);
        return (evt, artifact);
    }

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken)
        => events.QueryAsync(query, cancellationToken);

    public Task<AuditEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken)
        => events.GetAsync(eventId, cancellationToken);

    public async Task<AuditArtifactResult?> LoadArtifactAsync(
        string artifactId,
        bool includeImage,
        CancellationToken cancellationToken)
    {
        var artifact = await artifacts.LoadAsync(artifactId, cancellationToken);
        if (artifact is null)
            return null;

        return new AuditArtifactResult(
            artifact.Value.Metadata.ArtifactId,
            artifact.Value.Metadata.EventId,
            artifact.Value.Metadata.Kind,
            artifact.Value.Metadata.MimeType,
            artifact.Value.Metadata.SizeBytes,
            includeImage ? Convert.ToBase64String(artifact.Value.Bytes) : null);
    }

    private async Task<AuditIdentity> ResolveIdentityAsync(
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            return new AuditIdentity(null, null, null, null, null, null);

        var workspace = workspaces.GetById(workspaceId);
        return workspace is null
            ? new AuditIdentity(null, null, null, null, null, null)
            : await identities.ReadAsync(workspace, cancellationToken);
    }

    private static AuditEvent CreateEvent(
        AuditRecordRequest request,
        AuditIdentity identity,
        IReadOnlyList<string> artifactIds)
        => new(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            request.Kind,
            request.Source,
            request.Action,
            request.Outcome,
            request.WorkspaceId,
            identity.RepositoryPath,
            identity.WorktreePath,
            identity.WorkdirId,
            identity.Branch,
            identity.Commit,
            identity.Dirty,
            SanitizeDetails(request.Details),
            artifactIds);

    private static IReadOnlyDictionary<string, string> SanitizeDetails(
        IReadOnlyDictionary<string, string>? details)
    {
        if (details is null)
            return new Dictionary<string, string>();

        return details
            .Where(pair => !IsSensitive(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Length > 1000 ? pair.Value[..1000] : pair.Value,
                StringComparer.Ordinal);
    }

    private static bool IsSensitive(string key)
        => key.Contains("password", StringComparison.OrdinalIgnoreCase)
           || key.Contains("token", StringComparison.OrdinalIgnoreCase)
           || key.Contains("secret", StringComparison.OrdinalIgnoreCase);
}
