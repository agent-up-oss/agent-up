using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// Each kind of reviewer's queue on the Tasks board, sorted from what the Server holds: who has
/// each review, which kinds the open revision still needs, and what each kind already said.
/// </summary>
public sealed class TaskReviewQueueTests
{
    [Test]
    public void A_revision_nobody_of_the_kind_took_needs_a_reviewer_and_a_held_one_is_under_review()
    {
        var rows = TaskReviewQueue.Rows([Proposed("1"), Proposed("2") with { DevReviewerWorkspaceId = "hector" }], TaskReviewQueue.DevReviewer, Name);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => (row.Id, row.Action)), Is.EqualTo(new[] { ("1", "waiting"), ("2", "reviewing") }));
            Assert.That(rows.Select(row => row.Agent), Is.EqualTo(new[] { "Waiting for a reviewer · revision 2", "Hector · revision 2" }));
        });
    }

    [Test]
    public void A_kind_that_answered_the_open_revision_reads_as_reviewed_with_its_verdict()
    {
        var task = Proposed("3") with
        {
            PmReviewerWorkspaceId = "lucy",
            NeedsReview = [TaskReviewQueue.DevReviewer],
            Reviews = [new QueuedTaskReview("lucy", "requestChanges", 2, "The button does nothing.", TaskReviewQueue.PmReviewer)],
        };

        var row = TaskReviewQueue.Rows([task], TaskReviewQueue.PmReviewer, Name).Single();

        Assert.That((row.Action, row.Agent), Is.EqualTo(("reviewed", "Changes requested by Lucy · revision 2")));
    }

    // Every push needs reviewing again: a verdict on revision 1 says nothing about revision 2.
    [Test]
    public void A_review_of_an_older_revision_does_not_answer_the_open_one()
    {
        var task = Proposed("4") with
        {
            DevReviewerWorkspaceId = "hector",
            Reviews = [new QueuedTaskReview("hector", "approve", 1, "ok", TaskReviewQueue.DevReviewer)],
        };

        Assert.That(TaskReviewQueue.Rows([task], TaskReviewQueue.DevReviewer, Name).Single().Action, Is.EqualTo("reviewing"));
    }

    [Test]
    public void A_task_without_a_proposal_or_back_with_its_author_owes_no_review()
    {
        var rows = TaskReviewQueue.Rows(
            [Proposed("5") with { Stage = "implementing", Proposal = null }, Proposed("6") with { Stage = "changesRequested", NeedsReview = [] }],
            TaskReviewQueue.PmReviewer,
            Name);

        Assert.That(rows, Is.Empty);
    }

    private static string Name(string? id) => id switch { "hector" => "Hector", "lucy" => "Lucy", _ => id ?? "" };

    private static QueuedTask Proposed(string id) => new(
        id,
        "example",
        "repository",
        int.Parse(id),
        $"Task {id}",
        $"https://example.test/{id}",
        null,
        "bob",
        "inReview",
        new QueuedTaskProposal("7", "https://example.test/pulls/7", $"agent-up/issue-{id}", "main", 2),
        [],
        null,
        null,
        [TaskReviewQueue.DevReviewer, TaskReviewQueue.PmReviewer]);
}
