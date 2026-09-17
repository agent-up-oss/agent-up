export interface CommitQueueEntry {
  slice: string;
  message: string;
  files: string[];
  id: string;
  parentCommit: string | null;
  proposalCommit: string | null;
  state: string;
}

export interface CommitQueue {
  entries: CommitQueueEntry[];
  unassignedFiles: string[];
  queueWorktreePath: string | null;
  baseCommit: string | null;
  tipCommit: string | null;
  generation: number;
}
