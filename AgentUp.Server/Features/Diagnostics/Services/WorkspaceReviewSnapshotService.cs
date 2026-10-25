using System.ComponentModel;
using System.Text.Json;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Diagnostics.Models;
using AgentUp.Server.Features.Diagnostics.Providers;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Verification.Controllers;
using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Diagnostics.Services;

public sealed class WorkspaceReviewSnapshotService(
    WorkspaceQueryController workspaces,
    WorkspaceDiagnosticsController diagnostics,
    BrowserSessionController browser,
    AuditController audit,
    ReviewSnapshotHtmlProvider html,
    VerificationExportController? verification = null,
    WorkspaceOverviewController? overview = null,
    GitQueryController? git = null)
{
    private const int RecentActionLimit = 20;
    private const int ScreenshotQueryLimit = 50;
    private const int ScreenshotLimit = 10;
    private const string BrowserScreenshotAction = "browser_screenshot";

    public async Task<string?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var snapshot = await CaptureAsync(workspaceId, cancellationToken);
        return snapshot is null ? null : html.Render(snapshot);
    }

    private async Task<WorkspaceReviewSnapshot?> CaptureAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var diagnostic = await diagnostics.GetAsync(workspaceId, null, 200, 100, cancellationToken);
        var actions = await RecentActionsAsync(workspace.Id, cancellationToken);
        var screenshots = await ScreenshotsAsync(workspace.Id, cancellationToken);
        var metrics = overview?.Get(workspace.Id);
        var head = git is null ? null : await git.GetHeadAsync(workspace.Id, cancellationToken);
        var branch = !string.IsNullOrWhiteSpace(head?.Branch) ? head.Branch : workspace.Branch;
        var commit = !string.IsNullOrWhiteSpace(head?.Commit) ? head.Commit : workspace.Commit;
        return new WorkspaceReviewSnapshot(
            workspace.Id,
            workspace.DisplayName,
            workspace.RepositoryPath,
            workspace.WorktreePath,
            branch,
            commit,
            workspace.State.ToString(),
            diagnostic?.Health,
            DateTimeOffset.UtcNow,
            browser.ReadCurrentUrl(workspace.Id),
            metrics?.CpuPercent,
            metrics?.MemoryBytes,
            metrics?.StorageBytes,
            metrics?.ProcessCount,
            Applications(workspace, diagnostic),
            diagnostic?.Entries ?? [],
            actions,
            screenshots,
            await TryVerificationAsync(workspace.WorktreePath, cancellationToken));
    }

    private static IReadOnlyList<ReviewApplicationSnapshot> Applications(
        Workspace workspace,
        WorkspaceDiagnosticsDto? diagnostic)
    {
        var byName = (diagnostic?.Applications ?? [])
            .ToDictionary(app => app.Name, StringComparer.OrdinalIgnoreCase);
        return workspace.Applications
            .Select(app => ToApplication(app, byName.GetValueOrDefault(app.Name)))
            .ToArray();
    }

    private static ReviewApplicationSnapshot ToApplication(
        ApplicationInstance app,
        ApplicationDiagnosticsDto? diagnostic)
        => new(
            app.Name,
            app.ServiceType == ServiceType.Docker ? "Docker" : $"{app.Kind}",
            diagnostic?.ProcessState ?? $"{app.State}",
            diagnostic?.Health,
            diagnostic?.Readiness ?? ApplicationReadinessDescription.For(app),
            app.AllocatedPorts
                .Select(port => new ReviewPortSnapshot(port.Variable, port.AllocatedPort, port.Protocol))
                .ToArray(),
            diagnostic?.Logs ?? [],
            diagnostic?.LogsTruncated ?? false);

    private async Task<IReadOnlyList<ReviewActionSnapshot>> RecentActionsAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var events = await audit.QueryAsync(
            new AuditEventQuery(workspaceId, null, null, null, null, null, null, null, null, null, RecentActionLimit),
            cancellationToken);
        return events
            .Select(evt => new ReviewActionSnapshot(
                evt.EventId,
                evt.Timestamp,
                evt.Kind,
                evt.Source,
                evt.Action,
                evt.Outcome,
                Detail(evt, "message", "error", "url") ?? evt.Action))
            .ToArray();
    }

    private async Task<IReadOnlyList<ReviewScreenshotSnapshot>> ScreenshotsAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var events = await audit.QueryAsync(
            new AuditEventQuery(workspaceId, null, null, null, null, "browser", null, null, null, null, ScreenshotQueryLimit),
            cancellationToken);
        var shots = new List<ReviewScreenshotSnapshot>();
        foreach (var evt in events)
        {
            if (shots.Count >= ScreenshotLimit)
                break;
            if (!string.Equals(evt.Action, BrowserScreenshotAction, StringComparison.Ordinal) || evt.ArtifactIds.Count == 0)
                continue;

            var artifactId = evt.ArtifactIds[0];
            var artifact = await audit.LoadArtifactAsync(artifactId, includeImage: true, cancellationToken);
            shots.Add(new ReviewScreenshotSnapshot(
                evt.EventId,
                artifactId,
                evt.Timestamp,
                Detail(evt, "url"),
                artifact?.MimeType ?? Detail(evt, "mimeType"),
                artifact?.ImageBase64));
        }

        return shots;
    }

    private static string? Detail(AuditEvent evt, params string[] keys)
        => keys
            .Select(key => evt.Details.TryGetValue(key, out var value) ? value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private async Task<VerificationExportDto?> TryVerificationAsync(
        string worktreePath,
        CancellationToken cancellationToken)
    {
        if (verification is null || !Directory.Exists(worktreePath))
            return null;

        try
        {
            return await verification.ExportAsync(worktreePath, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or IOException
                or DirectoryNotFoundException
                or JsonException
                or UnauthorizedAccessException
                or ArgumentException
                or Win32Exception
                or VerificationConfigurationException)
        {
            return null;
        }
    }
}
