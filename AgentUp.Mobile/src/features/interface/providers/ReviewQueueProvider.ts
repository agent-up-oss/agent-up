import type { QueuedTask, ReviewKind } from '../models/TaskQueue';

/** Where a proposal stands with one kind of reviewer, as the review tab groups it. */
export type ReviewStanding = 'waiting' | 'reviewing' | 'reviewed';

export type ReviewRow = {
  task: QueuedTask;
  standing: ReviewStanding;
  /** Who: the reviewer holding this kind of review, or null while nobody of the kind took it. */
  reviewerId: string | null;
  revision: number;
  verdict: 'approve' | 'requestChanges' | 'comment' | null;
};

/**
 * One kind of reviewer's queue, read from what the Server holds. The host only counts approvals;
 * the Server says which kinds the open revision still needs and who holds each review, so this
 * only sorts: a revision nobody of the kind took needs a reviewer, one its reviewer holds is under
 * review, and one that kind already answered is reviewed. A task back with its author that this
 * kind has not reviewed owes nothing until the next push, so it is left out.
 */
export function reviewQueue(tasks: QueuedTask[], kind: ReviewKind): ReviewRow[] {
  const rows: ReviewRow[] = [];
  for (const task of tasks) {
    const proposal = task.proposal;
    if (!proposal || !task.stage || !['inReview', 'changesRequested', 'approved'].includes(task.stage)) continue;
    const reviewerId = (kind === 'devReviewer' ? task.devReviewerWorkspaceId : task.pmReviewerWorkspaceId) ?? null;
    const given = (task.reviews ?? []).find(review =>
      review.revision === proposal.revision && (review.kind ?? 'devReviewer') === kind && review.workspaceId === reviewerId);
    if (given) {
      rows.push({ task, standing: 'reviewed', reviewerId, revision: proposal.revision, verdict: given.verdict });
      continue;
    }
    if (!(task.needsReview ?? []).includes(kind) || task.stage !== 'inReview') continue;
    rows.push({ task, standing: reviewerId ? 'reviewing' : 'waiting', reviewerId, revision: proposal.revision, verdict: null });
  }
  return rows;
}

/** The line under a review row's title: who has it, which revision, and what they said. */
export function reviewLine(row: ReviewRow, reviewer: string): string {
  const revision = `revision ${row.revision}`;
  if (row.standing === 'waiting') return `Waiting for a reviewer · ${revision}`;
  if (row.standing === 'reviewing') return `${reviewer} · ${revision}`;
  const said = row.verdict === 'approve' ? 'Approved' : row.verdict === 'requestChanges' ? 'Changes requested' : 'Commented';
  return `${said} by ${reviewer} · ${revision}`;
}
