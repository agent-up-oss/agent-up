using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// One kind of reviewer's queue, read from what the Server holds. The host only counts approvals;
/// the Server says which kinds the open revision still needs and who holds each review, so this
/// only sorts: a revision nobody of the kind took needs a reviewer, one its reviewer holds is under
/// review, and one that kind already answered is reviewed. A task back with its author that this
/// kind has not reviewed owes nothing until the next push, so it is left out.
/// </summary>
public static class TaskReviewQueue
{
    public const string DevReviewer = "devReviewer";
    public const string PmReviewer = "pmReviewer";

    private static readonly HashSet<string> Proposed = new(StringComparer.Ordinal) { "inReview", "changesRequested", "approved" };

    public static IReadOnlyList<CommandCenterTaskSlot> Rows(IEnumerable<QueuedTask> tasks, string kind, Func<string?, string> name)
    {
        var rows = new List<CommandCenterTaskSlot>();
        foreach (var task in tasks.Where(task => task.Proposal is not null && task.Stage is not null && Proposed.Contains(task.Stage)))
        {
            var proposal = task.Proposal!;
            var reviewer = kind == DevReviewer ? task.DevReviewerWorkspaceId : task.PmReviewerWorkspaceId;
            var label = $"#{task.Number} · {task.Title}";
            var repository = $"{task.Owner}/{task.Repository}";
            var revision = $"revision {proposal.Revision}";
            var given = (task.Reviews ?? []).FirstOrDefault(review =>
                review.Revision == proposal.Revision && review.Kind == kind && review.WorkspaceId == reviewer);
            if (given is not null)
            {
                rows.Add(new CommandCenterTaskSlot(task.Id, label, repository, $"{Said(given.Verdict)} by {name(reviewer)} · {revision}", "reviewed"));
                continue;
            }

            if (task.Stage != "inReview" || !(task.NeedsReview ?? []).Contains(kind))
                continue;
            rows.Add(reviewer is null
                ? new CommandCenterTaskSlot(task.Id, label, repository, $"Waiting for a reviewer · {revision}", "waiting")
                : new CommandCenterTaskSlot(task.Id, label, repository, $"{name(reviewer)} · {revision}", "reviewing"));
        }

        return rows;
    }

    private static string Said(string verdict) => verdict switch
    {
        "approve" => "Approved",
        "requestChanges" => "Changes requested",
        _ => "Commented"
    };
}
