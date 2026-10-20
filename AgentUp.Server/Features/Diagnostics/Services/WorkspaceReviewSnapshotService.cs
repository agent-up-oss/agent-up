using System.ComponentModel;
using System.Text.Json;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.DTOs;
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
    VerificationExportController? verification = null)
{
    private const int RecentActionLimit = 20;

    public async Task<WorkspaceReviewSnapshotDto?> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var diagnostic = await diagnostics.GetAsync(workspaceId, null, 200, 100, cancellationToken);
        var actions = await RecentActionsAsync(workspace.Id, cancellationToken);
        return new WorkspaceReviewSnapshotDto(
            workspace.Id,
            workspace.DisplayName,
            workspace.RepositoryPath,
            workspace.WorktreePath,
            workspace.Branch,
            workspace.Commit,
            workspace.State.ToString(),
            diagnostic?.Health,
            DateTimeOffset.UtcNow,
            browser.ReadCurrentUrl(workspace.Id),
            Applications(workspace, diagnostic),
            diagnostic?.Entries ?? [],
            actions,
            await TryVerificationAsync(workspace.WorktreePath, cancellationToken));
    }

    private static IReadOnlyList<ReviewApplicationSnapshotDto> Applications(
        Workspace workspace,
        WorkspaceDiagnosticsDto? diagnostic)
    {
        var byName = (diagnostic?.Applications ?? [])
            .ToDictionary(app => app.Name, StringComparer.OrdinalIgnoreCase);
        return workspace.Applications
            .Select(app => ToApplication(app, byName.GetValueOrDefault(app.Name)))
            .ToArray();
    }

    private static ReviewApplicationSnapshotDto ToApplication(
        ApplicationInstance app,
        ApplicationDiagnosticsDto? diagnostic)
        => new(
            app.Name,
            app.ServiceType == ServiceType.Docker ? "Docker" : $"{app.Kind}",
            diagnostic?.ProcessState ?? $"{app.State}",
            diagnostic?.Health,
            app.AllocatedPorts
                .Select(port => new ReviewPortSnapshotDto(port.Variable, port.AllocatedPort, port.Protocol))
                .ToArray(),
            diagnostic?.Logs ?? [],
            diagnostic?.LogsTruncated ?? false);

    private async Task<IReadOnlyList<ReviewActionSnapshotDto>> RecentActionsAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var events = await audit.QueryAsync(
            new AuditEventQuery(workspaceId, null, null, null, null, null, null, null, null, null, RecentActionLimit),
            cancellationToken);
        return events
            .Select(evt => new ReviewActionSnapshotDto(
                evt.EventId,
                evt.Timestamp,
                evt.Kind,
                evt.Source,
                evt.Action,
                evt.Outcome))
            .ToArray();
    }

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
