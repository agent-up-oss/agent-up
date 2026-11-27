using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Adopts open host pull requests into the Server queue so an idle reviewer can take them.
/// Only the workspace's own repository is considered, because checkout happens in that clone.
/// A pull that already has a decisive review of this kind on the current head, that cannot
/// name its branches, or whose head lives on another repository, is left alone. Other kinds'
/// completed reviews of that same head travel with the adopted task so those kinds are not
/// offered again. A later push leaves those reviews behind, so every kind is asked again.
/// </summary>
public sealed class HostReviewQueueService(
    TaskQueueService queue,
    TaskSourceController source,
    ForgeController forge,
    IAgentWorkspaceSettingsRepository settings) : IHostReviewSync
{
    public async Task SyncAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var kind = settings.Get(workspaceId).Member;
        if (!TeamRoles.Reviews(kind))
            return;
        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return;
        var identity = source.Describe().Identities.FirstOrDefault(item =>
            item.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));
        if (identity is not { Connected: true, ModuleId.Length: > 0 })
            return;

        var pulls = await source.ListPullsAsync(identity.ModuleId, repository.Owner, repository.Repository, cancellationToken);

        var reviewKind = kind == TeamMemberKind.DevReviewer ? "devReviewer" : "pmReviewer";
        foreach (var pull in pulls)
        {
            var review = pull.Reviews.FirstOrDefault(item => item.Kind.Equals(reviewKind, StringComparison.Ordinal));
            if (review is null || review.Standing == "reviewed")
                continue;
            if (string.IsNullOrWhiteSpace(pull.HeadRef) || string.IsNullOrWhiteSpace(pull.BaseRef))
                continue;
            if (!string.Equals(pull.HeadOwner ?? repository.Owner, repository.Owner, StringComparison.OrdinalIgnoreCase))
                continue;
            var adopted = queue.AdoptProposal(ToTask(repository, pull));
            if (pull.HeadSha is { Length: > 0 })
                queue.Pushed(adopted.Owner, adopted.Repository, adopted.Id, pull.HeadSha);
        }
    }

    private static QueuedTask ToTask(ForgeRepositoryDto repository, TaskSourcePullDto pull) =>
        new(
            pull.Id,
            repository.Owner,
            repository.Repository,
            pull.Number,
            pull.Title,
            pull.Body ?? "",
            pull.Url,
            DateTimeOffset.UtcNow,
            Stage: TaskStage.InReview,
            Proposal: new TaskProposal(
                $"{pull.Number}",
                pull.Url,
                pull.HeadRef!,
                pull.BaseRef!,
                1,
                pull.HeadSha),
            Reviews: [.. pull.Reviews.Where(item => item.Standing == "reviewed").Select(HostReview)],
            BaseBranch: pull.BaseRef);

    private static TaskReview HostReview(TaskSourceReviewDto item) =>
        new(
            item.Login ?? item.Kind,
            item.Verdict is not null && item.Verdict.Contains("CHANGE", StringComparison.OrdinalIgnoreCase)
                ? TaskReviewVerdict.RequestChanges
                : TaskReviewVerdict.Approve,
            1,
            "",
            item.Kind.Equals("pmReviewer", StringComparison.Ordinal)
                ? TeamMemberKind.PmReviewer
                : TeamMemberKind.DevReviewer);
}
